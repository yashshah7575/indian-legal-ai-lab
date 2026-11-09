"""Document the sample that is already in sample-data/.

Re-running a full download needs network access to the public bucket
s3://indian-supreme-court-judgments (HTTPS endpoint in PublicBucketBootstrap).
This script only checks the committed manifest checksum.
"""
import hashlib
import pathlib
root = pathlib.Path(__file__).resolve().parents[1] / "sample-data"
data = (root / "manifest.json").read_bytes()
expected = (root / "manifest.sha256").read_text().split()[0]
actual = hashlib.sha256(data).hexdigest()
print("match" if actual == expected else "mismatch", actual)
