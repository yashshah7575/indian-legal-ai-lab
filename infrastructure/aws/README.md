# Optional AWS shape

The local application does not need AWS. If you later run ingestion in AWS, a minimal shape is:

- S3 for authorized PDF objects you are allowed to copy.
- SQS for the same job contract as `IngestionJob` (job id, case id, attempt count).
- A worker, which can be a container or Lambda, that calls the same chunk-and-index steps.
- A dead-letter queue after the retry count used by `IngestionPipeline`.

No Terraform stack is included. Docker is not installed on the machine used to build this repository, so `docker compose` was not executed here.
