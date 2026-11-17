---
tags: [skill-creator]
max_turns: 40
timeout_seconds: 1200
allowed_tools: [Read, Glob, Grep, Skill, Write, Edit, Bash]
---

Can you make me a diagram of how password reset works in our app? Flow: user clicks 'forgot password' on the React app, the Node API looks up the user in Postgres, creates a single-use token (we store only a SHA-256 hash, 30 min expiry), drops a message on our RabbitMQ queue, an email worker picks it up and sends through SendGrid with the link. User clicks the link, the app calls /reset with the token, API verifies it, updates the bcrypt hash and kills all their sessions in Redis. I want to share it with the security reviewer next week so they can see what data goes where. Save it as password-reset-flow.html
