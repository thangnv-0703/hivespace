---
tags: [skill-creator]
max_turns: 40
timeout_seconds: 1200
allowed_tools: [Read, Glob, Grep, Skill, Write, Edit, Bash]
---

We deploy like this: code lives in GitHub (3 repos: web, api, infra-terraform). GitHub Actions builds and runs tests + a Trivy scan, pushes images to ECR. Argo CD syncs to the EKS staging cluster automatically; prod needs a manual approval in GitHub environments, then Argo syncs prod. Terraform for infra runs plan on PR and apply after merge with approval. Datadog monitors both clusters and we roll back via Argo if the error rate spikes. Draw this as a nice interactive diagram page for our onboarding docs - save to delivery-pipeline.html
