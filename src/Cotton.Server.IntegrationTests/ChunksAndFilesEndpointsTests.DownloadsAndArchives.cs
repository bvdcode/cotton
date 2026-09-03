// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;
using FileVersionDto = Cotton.Files.FileVersionDto;

namespace Cotton.Server.IntegrationTests
{
    public partial class ChunksAndFilesEndpointsTests
    {
        [Test]
        public async Task Download_File_Works()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // resolve root node
            NodeDto? root = await _client!.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            // upload chunk
            byte[] content = Encoding.UTF8.GetBytes("download me");
            string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
            using MultipartFormDataContent form = new MultipartFormDataContent
            {
                {
                    new ByteArrayContent(content)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                    },
                    "file",
                    "chunk.bin"
                },
                { new StringContent(chunkHashLower), "hash" }
            };
            HttpResponseMessage upRes = await _client.PostAsync("/api/v1/chunks", form);
            if (!upRes.IsSuccessStatusCode)
            {
                throw new Exception($"Chunk upload failed with status code {upRes.StatusCode} and message: {await upRes.Content.ReadAsStringAsync()}");
            }
            upRes.EnsureSuccessStatusCode();

            // create file from chunk
            CreateFileFromChunksRequestDto fileReq = new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [chunkHashLower],
                Name = "download.txt",
                ContentType = "text/plain",
                Hash = chunkHashLower,
                NodeId = root!.Id
            };
            HttpResponseMessage createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
            createFileRes.EnsureSuccessStatusCode();

            // list children to get NodeFileId
            NodeContentDto? list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
            Assert.That(list, Is.Not.Null);
            NodeFileManifestDto? nodeFile = list!.Files.FirstOrDefault(f => f.Name == "download.txt");
            Assert.That(nodeFile, Is.Not.Null);

            // obtain tokenized download link and download file
            HttpResponseMessage linkResponse = await _client.GetAsync($"/api/v1/files/{nodeFile!.Id}/download-link");
            linkResponse.EnsureSuccessStatusCode();
            string downloadLink = (await linkResponse.Content.ReadAsStringAsync()).Trim().Trim('"');
            Assert.That(downloadLink, Is.Not.Null.And.Not.Empty);

            HttpResponseMessage dl = await _client.GetAsync(downloadLink);
            dl.EnsureSuccessStatusCode();
            byte[] bytes = await dl.Content.ReadAsByteArrayAsync();
            Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo("download me"));
        }

        [Test]
        public async Task Download_Archive_For_Selected_Files_Streams_Uncompressed_Zip_With_Utf8_Names()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto cyrillicFile = await UploadTextFileAsync(root!, "долги.txt", "рубли");
            NodeFileManifestDto notesFile = await UploadTextFileAsync(root!, "notes.txt", "plain notes");

            HttpResponseMessage linkResponse = await _client.PostAsJsonAsync("/api/v1/archives/download-link", new Cotton.Server.Models.Requests.CreateArchiveDownloadLinkRequest
            {
                FileIds = [cyrillicFile.Id, notesFile.Id],
                NodeIds = [],
                ArchiveName = "выгрузка",
            });
            linkResponse.EnsureSuccessStatusCode();
            ArchiveDownloadLinkDto? archive = await linkResponse.Content.ReadFromJsonAsync<Cotton.Server.Models.Dto.ArchiveDownloadLinkDto>();
            Assert.That(archive, Is.Not.Null);
            Assert.That(archive!.FileName, Is.EqualTo("выгрузка.zip"));

            HttpResponseMessage download = await _client.GetAsync(archive.Url);
            download.EnsureSuccessStatusCode();
            Assert.That(download.Content.Headers.ContentLength, Is.EqualTo(archive.SizeBytes));

            byte[] bytes = await download.Content.ReadAsByteArrayAsync();
            Assert.That(bytes.Length, Is.EqualTo(archive.SizeBytes));

            using ZipArchive zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            AssertZipEntry(zip, "долги.txt", "рубли");
            AssertZipEntry(zip, "notes.txt", "plain notes");
            Assert.That(zip.GetEntry("долги.txt")!.CompressedLength, Is.EqualTo(zip.GetEntry("долги.txt")!.Length));
        }

        [Test]
        public async Task Download_Archive_For_Folder_Includes_Nested_Files_And_Empty_Folders()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeDto folder = await CreateFolderAsync(root!.Id, "Папка");
            NodeDto nested = await CreateFolderAsync(folder.Id, "nested");
            _ = await CreateFolderAsync(folder.Id, "empty");
            await UploadTextFileAsync(folder, "root.txt", "root body");
            await UploadTextFileAsync(nested, "deep.txt", "deep body");

            HttpResponseMessage linkResponse = await _client.PostAsJsonAsync("/api/v1/archives/download-link", new Cotton.Server.Models.Requests.CreateArchiveDownloadLinkRequest
            {
                FileIds = [],
                NodeIds = [folder.Id],
            });
            linkResponse.EnsureSuccessStatusCode();
            ArchiveDownloadLinkDto? archive = await linkResponse.Content.ReadFromJsonAsync<Cotton.Server.Models.Dto.ArchiveDownloadLinkDto>();
            Assert.That(archive, Is.Not.Null);
            Assert.That(archive!.FileName, Is.EqualTo("Папка.zip"));

            HttpResponseMessage download = await _client.GetAsync(archive.Url);
            download.EnsureSuccessStatusCode();
            byte[] bytes = await download.Content.ReadAsByteArrayAsync();

            using ZipArchive zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            Assert.That(zip.GetEntry("Папка/"), Is.Not.Null);
            Assert.That(zip.GetEntry("Папка/empty/"), Is.Not.Null);
            Assert.That(zip.GetEntry("Папка/nested/"), Is.Not.Null);
            AssertZipEntry(zip, "Папка/root.txt", "root body");
            AssertZipEntry(zip, "Папка/nested/deep.txt", "deep body");
        }

        [Test]
        public async Task Download_Archive_Rejects_Another_Users_File()
        {
            string adminToken = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);
            NodeFileManifestDto file = await UploadTextFileAsync(root!, "private.txt", "secret");

            HttpResponseMessage createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
            {
                username = "archiveuser",
                password = "archivepass",
                role = UserRole.User,
            });
            createUserResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = null;
            string otherToken = await LoginAsync("archiveuser", "archivepass");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

            HttpResponseMessage linkResponse = await _client.PostAsJsonAsync("/api/v1/archives/download-link", new Cotton.Server.Models.Requests.CreateArchiveDownloadLinkRequest
            {
                FileIds = [file.Id],
                NodeIds = [],
            });

            Assert.That(linkResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Update_File_Metadata_Merges_Metadata_For_Own_File()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            NodeFileManifestDto file = await UploadTextFileAsync(
                root!,
                "metadata.txt",
                "metadata",
                new Dictionary<string, string>
                {
                    ["isClientEncrypted"] = "true",
                    ["originalContentType"] = "text/plain"
                });

            Dictionary<string, string> patch = new Dictionary<string, string>
            {
                ["en"] = "encrypted-display-name"
            };
            HttpResponseMessage updateRes = await _client.PatchAsJsonAsync($"/api/v1/files/{file.Id}/metadata", patch);
            updateRes.EnsureSuccessStatusCode();

            NodeFileManifestDto? updated = await updateRes.Content.ReadFromJsonAsync<NodeFileManifestDto>();
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.Metadata["isClientEncrypted"], Is.EqualTo("true"));
            Assert.That(updated.Metadata["originalContentType"], Is.EqualTo("text/plain"));
            Assert.That(updated.Metadata["en"], Is.EqualTo("encrypted-display-name"));
        }

        [Test]
        public async Task Create_File_From_Chunks_Detects_ContentType_From_FileName_When_Missing()
        {
            string token = await LoginAsync();
            _client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            NodeDto? root = await _client.GetFromJsonAsync<NodeDto>("/api/v1/layouts/resolver");
            Assert.That(root, Is.Not.Null);

            byte[] content = Encoding.UTF8.GetBytes("auto detect me");
            string chunkHashLower = Hasher.ToHexStringHash(Hasher.HashData(content));
            using MultipartFormDataContent form = new MultipartFormDataContent
            {
                {
                    new ByteArrayContent(content)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
                    },
                    "file",
                    "chunk.bin"
                },
                { new StringContent(chunkHashLower), "hash" }
            };
            HttpResponseMessage upRes = await _client.PostAsync("/api/v1/chunks", form);
            upRes.EnsureSuccessStatusCode();

            CreateFileFromChunksRequestDto fileReq = new CreateFileFromChunksRequestDto
            {
                ChunkHashes = [chunkHashLower],
                Name = "auto-detect.txt",
                ContentType = string.Empty,
                Hash = chunkHashLower,
                NodeId = root!.Id
            };
            HttpResponseMessage createFileRes = await _client.PostAsJsonAsync("/api/v1/files/from-chunks", fileReq);
            createFileRes.EnsureSuccessStatusCode();

            NodeContentDto? list = await _client.GetFromJsonAsync<NodeContentDto>($"/api/v1/layouts/nodes/{root!.Id}/children");
            Assert.That(list, Is.Not.Null);
            NodeFileManifestDto? file = list!.Files.FirstOrDefault(x => x.Name == "auto-detect.txt");
            Assert.That(file, Is.Not.Null);
            Assert.That(file!.ContentType, Is.EqualTo("text/plain"));
        }

    }
}
