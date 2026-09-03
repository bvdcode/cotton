// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net.Http.Headers;

namespace Cotton.Server.IntegrationTests
{
    public partial class MoveEndpointsTests
    {
        // ---------------------------------------------------------------------
        // MoveFile
        // ---------------------------------------------------------------------

        [Test]
        public async Task MoveFile_ToAnotherFolder_Succeeds()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto src = await CreateFolderAsync(root.Id, "src");
            NodeDto dst = await CreateFolderAsync(root.Id, "dst");
            NodeFileManifestDto file = await CreateFileAsync(src.Id, "doc.txt", "hello-1");

            HttpResponseMessage res = await MoveFileAsync(file.Id, dst.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            NodeContentDto children = await GetChildrenAsync(dst.Id);
            Assert.That(children.Files.Any(f => f.Id == file.Id), Is.True, "moved file must appear in destination");

            NodeContentDto srcChildren = await GetChildrenAsync(src.Id);
            Assert.That(srcChildren.Files.Any(f => f.Id == file.Id), Is.False, "moved file must not remain in source");
        }

        [Test]
        public async Task MoveFile_SameParent_IsNoOp()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "folder");
            NodeFileManifestDto file = await CreateFileAsync(folder.Id, "doc.txt", "hello-2");

            HttpResponseMessage res = await MoveFileAsync(file.Id, folder.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            NodeContentDto children = await GetChildrenAsync(folder.Id);
            Assert.That(children.Files.Count(f => f.Id == file.Id), Is.EqualTo(1));
        }

        [Test]
        public async Task MoveFile_NameCollisionWithSiblingFile_Returns409()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto src = await CreateFolderAsync(root.Id, "src");
            NodeDto dst = await CreateFolderAsync(root.Id, "dst");
            NodeFileManifestDto moving = await CreateFileAsync(src.Id, "doc.txt", "moving-content");
            await CreateFileAsync(dst.Id, "doc.txt", "blocker-content");

            HttpResponseMessage res = await MoveFileAsync(moving.Id, dst.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        }

        [Test]
        public async Task MoveFile_NameCollisionWithSiblingFolder_Returns409()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto src = await CreateFolderAsync(root.Id, "src");
            NodeDto dst = await CreateFolderAsync(root.Id, "dst");
            NodeFileManifestDto moving = await CreateFileAsync(src.Id, "thing", "moving-content");
            await CreateFolderAsync(dst.Id, "thing");

            HttpResponseMessage res = await MoveFileAsync(moving.Id, dst.Id);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        }

        [Test]
        public async Task MoveFile_TargetNotFound_Returns404()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto src = await CreateFolderAsync(root.Id, "src");
            NodeFileManifestDto file = await CreateFileAsync(src.Id, "doc.txt", "hello-3");

            HttpResponseMessage res = await MoveFileAsync(file.Id, Guid.NewGuid());
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task MoveFile_AcrossLayouts_Returns400()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto src = await CreateFolderAsync(root.Id, "src");
            NodeFileManifestDto file = await CreateFileAsync(src.Id, "doc.txt", "across-layouts");

            (Guid OwnerId, Guid RootId) additionalLayout = await CreateAdditionalLayoutRootAsync(
                _factory!.Services,
                "other-root");

            HttpResponseMessage res = await MoveFileAsync(file.Id, additionalLayout.RootId);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task MoveFile_EmptyParentId_Returns400()
        {
            await AuthenticateAsync();
            NodeDto root = await GetRootAsync();
            NodeDto folder = await CreateFolderAsync(root.Id, "src");
            NodeFileManifestDto file = await CreateFileAsync(folder.Id, "doc.txt", "hello-4");

            HttpResponseMessage res = await MoveFileAsync(file.Id, Guid.Empty);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

    }
}
