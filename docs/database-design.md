# Database design

The backend is configured for Entity Framework Core and SQL Server. Initial entity placeholders are in the Domain project, with the `ApplicationDbContext` in Infrastructure.

Before production use, define and test keys, foreign keys, tournament-scoped constraints, indexes, concurrency tokens and migrations. Store auction amounts as integer points or fixed-precision values; do not use floating point.
