CREATE EXTENSION vector;

CREATE TABLE public.file_embeddings (
    id uuid PRIMARY KEY,
    file_manifest_id uuid NOT NULL,
    fragment_index integer NOT NULL,
    index_version integer NOT NULL,
    embedding real[] NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX ix_embedding_fragment
    ON public.file_embeddings (file_manifest_id, index_version, fragment_index);

CREATE INDEX ix_file_embeddings_bge_m3_v1
    ON public.file_embeddings USING hnsw
    (((embedding)::public.vector(1024)) public.vector_cosine_ops)
    WHERE (index_version = 1);

INSERT INTO public.file_embeddings VALUES (
    '00000000-0000-0000-0000-000000000001',
    '00000000-0000-0000-0000-000000000002',
    0,
    1,
    array_fill(0.5::real, ARRAY[1024]),
    '2026-01-01T00:00:00Z',
    '2026-01-01T00:00:00Z'
);
