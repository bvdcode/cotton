// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.IntegrationTests
{
    public class PostgresRestoreListTests
    {
        [Test]
        public void Parse_RemovesOnlyManagedVectorObjects_AndPreservesOrder()
        {
            string archive = """
                ; Archive created by pg_dump
                4; 3079 12936743 EXTENSION - vector
                4099; 0 0 COMMENT - EXTENSION vector
                3898; 1259 12937071 INDEX public ix_file_embeddings_bge_m3_v1 cotton_client
                4100; 0 0 COMMENT public INDEX ix_file_embeddings_bge_m3_v1 cotton_client
                5; 3079 100 EXTENSION - hstore
                6; 0 0 COMMENT - EXTENSION hstore
                7; 1259 101 INDEX public ix_other cotton_client
                8; 1259 102 INDEX other ix_file_embeddings_bge_m3_v1 cotton_client
                9; 0 103 TABLE DATA public file_embeddings cotton_client
                10; 2606 104 FK CONSTRAINT public node_files foreign_key cotton_client
                """;
            PostgresRestoreList list = PostgresRestoreList.Parse(archive.Replace("\n", "\r\n"));
            Assert.Multiple(() =>
            {
                Assert.That(list.Result.HadVectorExtension, Is.True);
                Assert.That(list.Result.HadVectorIndex, Is.True);
                Assert.That(list.Content.Replace("\r\n", "\n").TrimEnd(), Is.EqualTo(string.Join('\n', archive.Split('\n').Where((_, index) => index is not (1 or 2 or 3 or 4)))));
            });
        }

        [TestCase("", false, false)]
        [TestCase("1; 3079 10 EXTENSION - vector", true, false)]
        [TestCase("1; 1259 10 INDEX public ix_file_embeddings_bge_m3_v1 owner", false, true)]
        [TestCase("1; 3079 10 EXTENSION - vector_extra", false, false)]
        [TestCase("; 1; 3079 10 EXTENSION - vector", false, false)]
        [TestCase("; 3079 10 EXTENSION - vector", false, false)]
        public void Parse_ReportsPreviousState(string archive, bool hadExtension, bool hadIndex)
        {
            PostgresRestoreList list = PostgresRestoreList.Parse(archive);
            Assert.Multiple(() =>
            {
                Assert.That(list.Result.HadVectorExtension, Is.EqualTo(hadExtension));
                Assert.That(list.Result.HadVectorIndex, Is.EqualTo(hadIndex));
            });
        }
    }
}
