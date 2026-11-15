# Deployment

Use environment-specific configuration for API origins, SQL Server connection strings, JWT signing keys and other secrets. Do not commit production credentials.

The frontend and backend scaffolds are not production-ready. Before deployment, configure HTTPS, restricted CORS, authentication and authorization, migrations, backups, logging and SignalR scale-out as needed.
