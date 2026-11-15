# Auction rules

The server must own all auction transitions and bid validation. A bid should be checked against the authenticated user's team membership, auction version, current bid, configured increment, purse and squad limits inside an atomic transaction. Publish accepted events only after persistence succeeds.

These rules are design requirements for the next implementation phase; they are not implemented by the scaffold.
