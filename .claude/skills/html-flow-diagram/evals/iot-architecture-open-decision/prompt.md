---
tags: [skill-creator]
max_turns: 40
timeout_seconds: 1200
allowed_tools: [Read, Glob, Grep, Skill, Write, Edit, Bash]
---

Turn this into an architecture flow diagram in HTML: 'Field devices (about 20k) send MQTT telemetry every 10s to AWS IoT Core. A rules engine forwards to Kinesis; a Lambda normalizes and writes to the time-series DB (we haven't decided between Timescale and InfluxDB yet). Alerts: a Flink job on Kinesis detects threshold breaches and sends to SNS, which notifies PagerDuty and the operator mobile app. Operators use a React dashboard behind Cognito that reads via API Gateway + Lambda. Raw payloads also land in S3 for 1 year.' Highlight the ingestion path and the alerting path separately, and make it obvious which bits are still undecided. Output: iot-architecture.html
