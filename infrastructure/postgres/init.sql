CREATE EXTENSION IF NOT EXISTS vector;
        CREATE TABLE IF NOT EXISTS judgment_chunks (
          chunk_id text PRIMARY KEY,
          case_id text NOT NULL,
          court text NOT NULL,
          year int NOT NULL,
          content text NOT NULL,
          embedding vector(64) NOT NULL
        );
