# Supermarket Management System

This workspace now includes:

- `sms-frontend`: Angular 21 frontend using Angular Material and a custom theme.
- `sms-api`: Express API with persistent JSON data storage (`sms-api/db.json`).

## Run the full system

From this folder:

```bash
npm start
```

This runs:

- API: `http://localhost:3000`
- Frontend: `http://localhost:4200`

## Individual services

```bash
npm run start:api
npm run start:web
```

## Build frontend

```bash
npm run build:web
```
