﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.SemanticMemory.Core20;

public class SemanticMemoryWebClient : ISemanticMemoryClient
{
    private readonly HttpClient _client;

    public SemanticMemoryWebClient(string endpoint) : this(endpoint, new HttpClient())
    {
    }

    public SemanticMemoryWebClient(string endpoint, HttpClient client)
    {
        this._client = client;
        this._client.BaseAddress = new Uri(endpoint);
    }

    public Task ImportFileAsync(string file, ImportFileOptions options)
    {
        return this.ImportFilesInternalAsync(new[] { file }, options);
    }

    public Task ImportFilesAsync(string[] files, ImportFileOptions options)
    {
        return this.ImportFilesInternalAsync(files, options);
    }

    private async Task ImportFilesInternalAsync(string[] files, ImportFileOptions options)
    {
        options.Sanitize();
        options.Validate();

        // Populate form with values and files from disk
        using var formData = new MultipartFormDataContent();

        using var requestIdContent = new StringContent(options.RequestId);
        using (var userContent = new StringContent(options.UserId))
        {
            List<IDisposable> disposables = new();
            formData.Add(requestIdContent, "requestId");
            formData.Add(userContent, "user");
            foreach (var vaultId1 in options.VaultIds)
            {
                var content = new StringContent(vaultId1);
                disposables.Add(content);
                formData.Add(content, "vaults");
            }

            for (int index = 0; index < files.Length; index++)
            {
                string filename = files[index];
                byte[] bytes = File.ReadAllBytes(filename);
                var content = new ByteArrayContent(bytes, 0, bytes.Length);
                disposables.Add(content);
                formData.Add(content, $"file{index + 1}", filename);
            }

            // Send HTTP request
            try
            {
                HttpResponseMessage? response = await this._client.PostAsync("/upload", formData).ConfigureAwait(false);
                formData.Dispose();
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e) when (e.Data.Contains("StatusCode"))
            {
                throw new SemanticMemoryWebException($"{e.Message} [StatusCode: {e.Data["StatusCode"]}]", e);
            }
            catch (Exception e)
            {
                throw new SemanticMemoryWebException(e.Message, e);
            }
            finally
            {
                foreach (var disposable in disposables)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
