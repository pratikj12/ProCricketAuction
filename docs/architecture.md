# Architecture

The initial project layout is a modular monolith:

```text
React + Vite frontend -> ASP.NET Core API -> Application services -> EF Core -> SQL Server
                                                        \-> SignalR auction hub
```

The frontend must treat the API as the authority for identity, permissions, bids, auction state and purse calculations. The starter scaffold does not yet implement those business rules.
