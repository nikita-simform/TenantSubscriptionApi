#!/usr/bin/env bash
# Deploys the React build to an S3 bucket inside LocalStack, configured for static
# website hosting — the classic, genuinely-common real-AWS pattern for serving an SPA.
# Everything here is Community-edition S3 only; no ECS/RDS emulation involved.
set -euo pipefail

BUCKET_NAME="${1:-tenant-subscription-frontend}"
LOCALSTACK_ENDPOINT="http://localhost:4566"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# LocalStack doesn't validate these — any non-empty values work. Scoped to this
# script's own environment, not written to ~/.aws/, so it never touches real AWS
# credentials you might configure later.
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_DEFAULT_REGION=us-east-1

awslocal() { aws --endpoint-url="$LOCALSTACK_ENDPOINT" "$@"; }

echo "==> Waiting for LocalStack to be healthy..."
until curl -sf "$LOCALSTACK_ENDPOINT/_localstack/health" | grep -q '"s3": "available"'; do
  sleep 2
done
echo "    LocalStack is up."

echo "==> Building the React app (pointed at the API on :8080)..."
(cd "$REPO_ROOT/frontend" && VITE_API_BASE_URL="http://localhost:8080" npm run build)

echo "==> Creating bucket s3://$BUCKET_NAME ..."
awslocal s3 mb "s3://$BUCKET_NAME" 2>/dev/null || echo "    (already exists)"

echo "==> Configuring static website hosting..."
awslocal s3 website "s3://$BUCKET_NAME" --index-document index.html --error-document index.html

echo "==> Applying a public-read bucket policy (required for a website endpoint to be viewable)..."
cat > /tmp/bucket-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [{
    "Sid": "PublicReadGetObject",
    "Effect": "Allow",
    "Principal": "*",
    "Action": "s3:GetObject",
    "Resource": "arn:aws:s3:::$BUCKET_NAME/*"
  }]
}
EOF
awslocal s3api put-bucket-policy --bucket "$BUCKET_NAME" --policy file:///tmp/bucket-policy.json

echo "==> Uploading the build..."
awslocal s3 sync "$REPO_ROOT/frontend/dist" "s3://$BUCKET_NAME" --delete

echo
echo "Done. Site is served at:"
echo "  http://$BUCKET_NAME.s3-website.localhost.localstack.cloud:4566"
echo "(requires DNS resolution of *.localstack.cloud, a wildcard LocalStack publishes"
echo " to resolve to 127.0.0.1 — if that doesn't resolve on this network, fall back to:"
echo "  http://localhost:4566/$BUCKET_NAME/index.html  — works, but relative asset paths"
echo "  inside the built index.html may not resolve correctly via this path-style URL.)"
