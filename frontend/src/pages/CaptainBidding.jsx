import { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeft, ArrowUpRight, Check, Clock3, ShieldCheck, Users } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { formatMoney } from '../context/AuctionDataContext.jsx';
import useAuction from '../hooks/useAuction.js';

function initials(name = '') {
  return name.split(' ').map((part) => part[0]).slice(0, 2).join('');
}

export default function CaptainBidding() {
  const { auctionId } = useParams();
  const { auction, loading, error, pendingAction, connected, connectionError, remainingSeconds, placeBid } = useAuction(auctionId);
  const [teamId, setTeamId] = useState('');
  const [bidAmount, setBidAmount] = useState('');
  const lastRequest = useRef(null);
  const assignedTeams = useMemo(() => {
    if (!auction) return [];
    const assignedIds = new Set(auction.assignedTeamIds ?? []);
    return auction.teams.filter((team) => assignedIds.has(team.id));
  }, [auction]);
  const selectedTeam = assignedTeams.find((team) => team.id === teamId)
    ?? assignedTeams.find((team) => team.id === auction?.assignedTeamId)
    ?? assignedTeams[0];
  const player = auction?.currentPlayer;
  const nextBid = auction?.nextBidAmount ?? 0;
  const leader = auction?.teams.find((team) => team.id === auction.leadingTeamId);
  const amount = Number(bidAmount);
  const validAmount = Number.isSafeInteger(amount) && amount >= nextBid;
  const canBid = Boolean(auction && player && selectedTeam && auction.status === 'Live'
    && remainingSeconds > 0 && selectedTeam.squadSlotsRemaining > 0 && validAmount && amount <= selectedTeam.purseRemaining
    && selectedTeam.id !== auction.leadingTeamId && !pendingAction);

  useEffect(() => {
    if (!selectedTeam) {
      setTeamId('');
      return;
    }
    setTeamId(selectedTeam.id);
  }, [selectedTeam?.id]);

  useEffect(() => {
    setBidAmount(String(nextBid || ''));
    lastRequest.current = null;
  }, [nextBid, player?.id, auction?.revision, selectedTeam?.id]);

  async function submitBid() {
    if (!canBid || !auction || !selectedTeam) return;
    const requestFingerprint = `${auction.revision}:${selectedTeam.id}:${amount}`;
    const clientRequestId = lastRequest.current?.fingerprint === requestFingerprint
      ? lastRequest.current.id
      : crypto.randomUUID();
    lastRequest.current = { fingerprint: requestFingerprint, id: clientRequestId };
    try {
      await placeBid({
        teamId: selectedTeam.id,
        amount,
        expectedRevision: auction.revision,
        clientRequestId
      });
      lastRequest.current = null;
      toast.success(`Bid accepted: ${formatMoney(amount, auction.currencyCode)}`);
    } catch (requestError) {
      toast.error(requestError.response?.data?.detail || requestError.response?.data?.title
        || requestError.message || 'The bid was not accepted.');
    }
  }

  if (loading && !auction) return <main className="captain-page"><div className="captain-content">Loading live auction…</div></main>;
  if (!auction) return <main className="captain-page"><div className="captain-content"><section className="panel workspace-error" role="alert">{error || 'This auction is unavailable to your account.'}</section><Link to="/auction/events">Back to auction events</Link></div></main>;

  return (
    <main className="captain-page">
      <header className="captain-topbar">
        <Link to="/auction/events" className="captain-back"><ArrowLeft size={17} /> Auction events</Link>
        <span className="captain-brand">ProCricket<span>.</span></span>
        <span className={`captain-state ${auction.status.toLowerCase()}`}><i /> {auction.status === 'Live' ? 'LIVE AUCTION' : auction.status.toUpperCase()}</span>
      </header>
      <div className="captain-content">
        <div className="captain-welcome">
          <div className="eyebrow"><span className="eyebrow-line" />CAPTAIN’S BID DESK · {auction.auctionName}</div>
          <h1>Your next pick<br />could be <span>the one.</span></h1>
          <p>{auction.tournamentName} · {auction.seasonName}. Bids are validated and ordered by the auction server.</p>
          <span className={`connection-state ${connected ? 'is-connected' : 'is-disconnected'}`}><i /> {connected ? 'Connected to live auction' : 'Reconnecting to live auction'}</span>
          {connectionError && <p role="status">{connectionError}</p>}
        </div>

        {!assignedTeams.length ? <section className="panel captain-empty">
          <span className="empty-icon"><Users size={20} /></span><h2>No team is assigned to your account</h2>
          <p>Ask the tournament organizer to assign your registered account to a captain team before bidding.</p>
        </section> : <>
          <div className="captain-team-select">
            <div className="team-select-icon"><Users size={19} /></div>
            <label><small>YOUR ASSIGNED TEAM{assignedTeams.length > 1 ? 'S' : ''}</small>
              {assignedTeams.length > 1
                ? <select value={selectedTeam?.id ?? ''} onChange={(event) => setTeamId(event.target.value)}>{assignedTeams.map((team) => <option key={team.id} value={team.id}>{team.name} · {team.shortName}</option>)}</select>
                : <strong>{selectedTeam?.name} · {selectedTeam?.shortName}</strong>}
            </label>
            <span className="captain-purse">{formatMoney(selectedTeam?.purseRemaining, auction.currencyCode)}<small>AVAILABLE PURSE</small></span>
          </div>

          {player ? <>
            <div className="captain-player-card">
              <div className="captain-player-art">
                {player.photoDataUrl ? <img src={player.photoDataUrl} alt={player.name} /> : <div className="captain-player-initials">{initials(player.name)}</div>}
                <span>ON THE BLOCK <i /></span>
              </div>
              <div className="captain-player-info">
                <span className="stage-role">{player.role.toUpperCase()} · {player.registrationNumber}</span>
                <h2>{player.name}</h2><p>{player.city || 'Registered player'} · {player.status}</p>
                <div className="captain-prices">
                  <div><small>BASE PRICE</small><b>{formatMoney(player.basePrice, auction.currencyCode)}</b></div>
                  <div><small>TOP BID</small><b>{formatMoney(auction.currentBidAmount || player.basePrice, auction.currencyCode)}</b><span>{leader ? `by ${leader.shortName}` : 'No bids yet'}</span></div>
                </div>
              </div>
            </div>
            <div className="next-bid-box">
              <div><span>NEXT VALID BID</span><strong>{formatMoney(nextBid, auction.currencyCode)}</strong></div>
              <span className="captain-timer"><Clock3 size={15} /> {remainingSeconds}s · increment {formatMoney(auction.bidIncrement, auction.currencyCode)}</span>
            </div>
            <label className="captain-bid-amount-label">Your bid amount ({auction.currencyCode})
              <input aria-label="Your bid amount" type="number" min={nextBid} max={selectedTeam?.purseRemaining} step="1" value={bidAmount} disabled={Boolean(pendingAction)} onChange={(event) => setBidAmount(event.target.value)} />
              <small>Bid at least the next valid amount or jump higher, up to your available purse. Advisory reserve limit: {formatMoney(selectedTeam?.maximumAffordableBid, auction.currencyCode)}.</small>
            </label>
            <button className="captain-bid-button" disabled={!canBid} onClick={() => void submitBid()}>
              <span><ArrowUpRight size={21} /></span>
              {pendingAction === 'bid' ? 'SUBMITTING BID…' : selectedTeam?.id === auction.leadingTeamId ? 'YOUR TEAM IS LEADING' : `BID ${formatMoney(amount || nextBid, auction.currencyCode)}`}
              <small>{pendingAction === 'bid' ? 'Waiting for server confirmation' : auction.status !== 'Live' ? 'Waiting for auctioneer to open bidding' : !selectedTeam?.squadSlotsRemaining ? 'Your team has reached its squad limit' : !validAmount ? 'Enter a bid at least as high as the next valid bid' : amount > (selectedTeam?.purseRemaining ?? 0) ? 'Bid exceeds your available purse' : 'Tap once to submit securely'}</small>
            </button>
            {error && <div className="workspace-error" role="alert">{error}</div>}
            <div className="captain-safety"><ShieldCheck size={17} /><span><b>Server-authoritative bidding.</b> Team assignment, timing, bid order, purse, and squad size are checked before acceptance.</span><Check size={16} /></div>
          </> : <div className="captain-empty"><span className="empty-icon"><Check size={20} /></span><h2>Waiting for the next player</h2><p>The auctioneer will present an eligible player. Your team purse and squad slots remain unchanged until a sale is confirmed.</p></div>}

          {!!auction.recentBids?.length && <section className="panel recent-bids-panel">
            <div className="panel-heading"><div><h3>Recent accepted bids</h3><p>Ordered by server sequence</p></div></div>
            <div className="mini-bid-list">{auction.recentBids.map((bid) => <div className="mini-bid" key={bid.id}><span>#{bid.sequenceNumber} · {bid.teamName}</span><b>{formatMoney(bid.amount, auction.currencyCode)}</b></div>)}</div>
          </section>}
        </>}
        <div className="captain-quick-links"><Link to={`/auction/live/${auctionId}`}>Auctioneer control room <ArrowUpRight size={14} /></Link><Link to={`/auction/display/${auctionId}`}>Projector display <ArrowUpRight size={14} /></Link></div>
      </div>
    </main>
  );
}
