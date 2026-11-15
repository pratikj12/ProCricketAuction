# API documentation

The ASP.NET Core API exposes versioned routes under `/api/v1`. In Development, the OpenAPI document is available at `/openapi/v1.json`.

## Public live-auction display

The following read-only endpoints require no account or login:

- `GET /api/v1/public/auctions/{auctionId}` returns the public display state for a started auction. Scheduled and cancelled auctions are not available through this endpoint.
- `GET /hubs/public-auction` is the anonymous SignalR hub. Invoke `JoinPublicAuction(auctionId)` to receive the current public state and committed auction updates.

The public state includes live player and bid details, team purse summaries, and recent sales. It omits team assignments for the connected user, bidding permissions, affordable-bid recommendations, the player roster beyond the active/recently resolved player, and the audit trail. Public viewers cannot submit bids or control the auction.

The shareable browser route is `/auction/watch/{auctionId}`. It is a read-only projector-style view; use the authenticated auctioneer and captain routes for controls and bidding.

## Authenticated auction commands

Auctioneer and captain operations use authenticated endpoints under `/api/v1/auctions/{auctionId}`. Bid, timer, player, sale, and correction commands are validated and persisted by the server. See `backend/ProCricketAuction.Api.http` for request examples.
