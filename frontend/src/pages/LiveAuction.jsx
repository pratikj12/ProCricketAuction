import { Check, CirclePause, CirclePlay, Copy, Expand, ExternalLink, Gavel, Mail, MessageCircle, MessageSquare, Share2, SkipForward, Timer, Users, X } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { useState } from 'react';
import toast from 'react-hot-toast';
import useAuction from '../hooks/useAuction.js';
import { formatMoney } from '../context/AuctionDataContext.jsx';

function initials(name = '') {
  return name.split(' ').map((part) => part[0]).slice(0, 2).join('');
}

function showError(error) {
  toast.error(error.response?.data?.detail || error.response?.data?.title
    || error.message || 'The auction action could not be completed.');
}

export default function LiveAuction() {
  const { auctionId } = useParams();
  const [shareMenuOpen, setShareMenuOpen] = useState(false);
  const {
    auction,
    loading,
    error,
    pendingAction,
    connected,
    connectionError,
    remainingSeconds,
    refresh,
    start,
    pause,
    resume,
    nextPlayer,
    skipPlayer,
    markSold,
    markUnsold,
    startNextRound,
    reverseSale
  } = useAuction(auctionId);

  if (loading && !auction) return <main className="auctioneer-page"><section className="panel table-empty">Loading authoritative auction state…</section></main>;
  if (!auction) return <main className="auctioneer-page"><section className="panel workspace-error" role="alert">{error || 'This auction is unavailable to your account.'}<button className="button button-ghost" onClick={() => void refresh().catch(() => {})}>Retry</button></section></main>;

  const { teams, players, currentPlayer: player } = auction;
  const leader = teams.find((team) => team.id === auction.leadingTeamId);
  const availableCount = players.filter((item) => item.status === 'Available').length;
  const soldCount = players.filter((item) => item.status === 'Sold').length;
  const recentBids = auction.recentBids ?? [];
  const busy = Boolean(pendingAction);
  const run = (action) => action().catch(showError);
  const publicAuctionUrl = `${window.location.origin}/auction/watch/${encodeURIComponent(auctionId)}`;
  const shareMessage = `Watch the live ${auction?.auctionName || 'cricket auction'}: ${publicAuctionUrl}`;
  const copyPublicLink = async () => {
    try {
      await navigator.clipboard.writeText(publicAuctionUrl);
      toast.success('Public auction link copied.');
      setShareMenuOpen(false);
    } catch (copyError) {
      toast.error(copyError.message || 'Could not copy the auction link.');
    }
  };
  const finalizeSale = () => {
    if (!leader || !player) return;
    if (window.confirm(`Confirm ${player.name} SOLD to ${leader.name} for ${formatMoney(auction.currentBidAmount, auction.currencyCode)}?`)) {
      void run(() => markSold(auction.revision));
    }
  };
  const canFinalize = Boolean(player) && ['Live', 'AwaitingDecision'].includes(auction.status);

  return (
    <main className="auctioneer-page">
      <div className="auction-page-head">
        <div>
          <div className="eyebrow"><span className="eyebrow-line" />{auction.status === 'Live' ? <><span className="live-pip" /> LIVE AUCTION</> : 'AUCTION CONTROL ROOM'}</div>
          <h1>On the block<span className="head-period">.</span></h1>
          <p className="subheading">{auction.tournamentName} · {auction.seasonName} · {auction.auctionName}</p>
          <span className={`connection-state ${connected ? 'is-connected' : 'is-disconnected'}`}><i /> {connected ? 'Live sync connected' : 'Reconnecting to live sync'}</span>
          {connectionError && <p className="workspace-error" role="status">{connectionError}</p>}
        </div>
        <div className="auction-head-actions">
          <Link to={`/auction/bid/${auctionId}`} className="button button-ghost"><Gavel size={15} /> Captain bid desk</Link>
          <Link to={`/auction/display/${auctionId}`} target="_blank" className="button button-ghost"><Expand size={15} /> Projector view <ExternalLink size={13} /></Link>
          {['Live', 'Paused', 'AwaitingDecision', 'Completed'].includes(auction.status) && <div className="auction-share">
            <button className="button button-green" type="button" aria-expanded={shareMenuOpen} onClick={() => setShareMenuOpen((open) => !open)}>
              <Share2 size={15} /> Share live auction
            </button>
            {shareMenuOpen && <div className="auction-share-menu" role="group" aria-label="Share public auction link">
              {typeof navigator.share === 'function' && <button type="button" onClick={() => {
                void navigator.share({ title: `${auction.auctionName} · ProCricket Auction`, text: `Watch the live ${auction.auctionName}.`, url: publicAuctionUrl })
                  .then(() => setShareMenuOpen(false))
                  .catch((shareError) => {
                    if (shareError.name !== 'AbortError') toast.error(shareError.message || 'Could not share the auction link.');
                  });
              }}><Share2 size={15} /> More sharing options</button>}
              <a href={`https://wa.me/?text=${encodeURIComponent(shareMessage)}`} target="_blank" rel="noreferrer" onClick={() => setShareMenuOpen(false)}>
                <MessageCircle size={15} /> WhatsApp
              </a>
              <a href={`sms:?body=${encodeURIComponent(shareMessage)}`} onClick={() => setShareMenuOpen(false)}>
                <MessageSquare size={15} /> Text message
              </a>
              <a href={`mailto:?subject=${encodeURIComponent(`Watch ${auction.auctionName}`)}&body=${encodeURIComponent(shareMessage)}`} onClick={() => setShareMenuOpen(false)}>
                <Mail size={15} /> Email
              </a>
              <button type="button" onClick={() => void copyPublicLink()}><Copy size={15} /> Copy public link</button>
              <span>Anyone with this link can view this auction, but cannot bid or manage it.</span>
            </div>}
          </div>}
          {auction.status === 'Scheduled' && <button className="button button-green" disabled={busy} onClick={() => void run(start)}><CirclePlay size={16} /> Start auction</button>}
          {auction.status === 'Live' && <button className="button button-warning" disabled={busy} onClick={() => void run(pause)}><CirclePause size={16} /> Pause auction</button>}
          {auction.status === 'Paused' && <button className="button button-green" disabled={busy} onClick={() => void run(resume)}><CirclePlay size={16} /> Resume auction</button>}
        </div>
      </div>

      {error && <div className="panel workspace-error" role="alert">{error}</div>}
      <div className="auctioner-layout">
        <section className="auction-stage">
          <div className="stage-topline">
            <span><span className="stage-live-dot" /> {auction.status.toUpperCase()}</span>
            <span>ROUND {auction.currentRound} <i>/</i> {auction.maximumRounds} · {soldCount} SOLD <i>/</i> {players.length}</span>
          </div>
          {player ? <>
            <div className="player-stage">
              <div className="stage-glow" /><div className="stage-orbit" />
              <div className="stage-avatar">
                {player.photoDataUrl ? <img src={player.photoDataUrl} alt={player.name} /> : <span>{initials(player.name)}</span>}
                <small>{player.registrationNumber}</small>
              </div>
              <div className="stage-player-info">
                <span className="stage-role">{player.role.toUpperCase()}</span><h2>{player.name}</h2>
                <p>{player.city || 'Registered player'} <i /> {player.status} <i /> {player.registrationNumber}</p>
                <div className="stage-price-row">
                  <div><small>BASE PRICE</small><b>{formatMoney(player.basePrice, auction.currencyCode)}</b></div>
                  <div className="price-divider" />
                  <div><small>CURRENT BID</small><b className="current-price">{formatMoney(auction.currentBidAmount || player.basePrice, auction.currencyCode)}</b></div>
                </div>
              </div>
              <div className="timer-widget"><Timer size={16} /><span>TIME REMAINING</span><b className={remainingSeconds < 10 ? 'timer-low' : ''}>00:{String(remainingSeconds).padStart(2, '0')}</b><div className="timer-line"><i style={{ width: `${auction.bidDurationSeconds ? remainingSeconds / auction.bidDurationSeconds * 100 : 0}%` }} /></div></div>
            </div>
            <div className={`leading-banner ${leader ? '' : 'no-leader'}`}>
              <span className="leading-crown">♛</span>
              <span>{leader ? <><small>CURRENTLY LEADING</small><b>{leader.name}</b></> : <><small>OPEN FOR BIDDING</small><b>No bids yet</b></>}</span>
              <strong>{formatMoney(auction.currentBidAmount || player.basePrice, auction.currencyCode)}</strong>
            </div>
            <div className="control-row">
              <button className="button button-ghost" disabled={busy || !canFinalize} onClick={() => void run(markUnsold)}><X size={16} /> Mark unsold</button>
              <button className="button button-ghost" disabled={busy || !canFinalize} onClick={() => void run(skipPlayer)}><SkipForward size={16} /> Skip & advance</button>
              <button className="button button-green" disabled={busy || !leader || !canFinalize} onClick={finalizeSale}><Check size={16} /> SOLD to {leader?.shortName || '—'}</button>
            </div>
          </> : <div className="no-player">
            <div className="empty-icon"><Check size={23} /></div>
            <h2>{auction.status === 'Completed' ? 'Auction complete' : 'Ready for the next player?'}</h2>
            <p>{auction.status === 'Completed'
              ? 'The current round has no remaining players.'
              : `${availableCount} available players · ${auction.status === 'AwaitingDecision' ? 'Resolve the active result before continuing.' : 'Bring the next eligible player onto the block.'}`}</p>
            {auction.status === 'Live' && <button className="button button-green" disabled={busy} onClick={() => void run(nextPlayer)}><SkipForward size={16} /> {availableCount ? 'Present next player' : 'Complete current round'}</button>}
            {auction.status === 'Completed' && auction.currentRound < auction.maximumRounds && players.some((item) => item.status === 'Unsold') && <button className="button button-green" disabled={busy} onClick={() => void run(startNextRound)}><SkipForward size={16} /> Start round {auction.currentRound + 1}</button>}
          </div>}
        </section>

        <aside className="auction-sidebar">
          <div className="panel bids-panel">
            <div className="panel-heading"><div><h3>Team purses</h3><p>Confirmed spend and squad capacity</p></div><Users size={17} /></div>
            <div className="auction-team-list">{teams.map((team, index) => {
              const leading = team.id === auction.leadingTeamId;
              const spentRatio = team.initialPurse ? Math.min(100, team.amountSpent / team.initialPurse * 100) : 0;
              return <div className={`auction-team-row ${leading ? 'is-leading' : ''}`} key={team.id}>
                <div className={`team-mark team-mark-${index + 1}`} style={{ '--team': team.color }}>{team.shortName}</div>
                <div className="auction-team-meta">
                  <b>{team.name}{leading && <span className="leader-tag">LEADING</span>}</b>
                  <div className="purse-track"><i style={{ width: `${spentRatio}%`, background: team.color }} /></div>
                  <small>{formatMoney(team.purseRemaining, auction.currencyCode)} left · {team.playersPurchased} bought · {team.squadSlotsRemaining} slots</small>
                </div>
              </div>;
            })}</div>
          </div>

          <div className="panel recent-bids-panel">
            <div className="panel-heading"><div><h3>Accepted bids</h3><p>Server-ordered activity for this player</p></div><span className="live-tag">{auction.acceptedBidCount} BIDS</span></div>
            {recentBids.length ? <div className="mini-bid-list">{recentBids.map((bid) => <div className="mini-bid" key={bid.id}><span>#{bid.sequenceNumber} · {bid.teamName}</span><b>{formatMoney(bid.amount, auction.currencyCode)}</b></div>)}</div> : <div className="table-empty">Accepted bids will appear here.</div>}
          </div>

          <div className="panel recent-bids-panel">
            <div className="panel-heading"><div><h3>Recent confirmed sales</h3><p>Sale corrections are audited and purse-reconciled</p></div></div>
            {auction.recentSales?.length ? <div className="mini-bid-list">{auction.recentSales.map((sale) => <div className="mini-bid" key={sale.id}>
              <span>{sale.playerName} · {sale.teamName} · {formatMoney(sale.price, auction.currencyCode)}</span>
              {!sale.isReversed && <button className="button button-ghost" disabled={busy} onClick={() => {
                const reason = window.prompt(`Reason for reversing ${sale.playerName}'s sale?`);
                if (!reason?.trim()) return;
                if (window.confirm(`Reverse the ${formatMoney(sale.price, auction.currencyCode)} sale and return the player to the pool?`)) {
                  void run(() => reverseSale({ purchaseId: sale.id, reason: reason.trim(), expectedRevision: auction.revision }));
                }
              }}>Reverse</button>}
              {sale.isReversed && <small>REVERSED</small>}
            </div>)}</div> : <div className="table-empty">No confirmed sales yet.</div>}
          </div>

          <div className="panel recent-bids-panel">
            <div className="panel-heading"><div><h3>Audit trail</h3><p>Server-ordered auction actions</p></div></div>
            {auction.recentAuditEvents?.length ? <div className="mini-bid-list">{auction.recentAuditEvents.slice(0, 8).map((entry) => <div className="mini-bid" key={entry.sequenceNumber}><span>#{entry.sequenceNumber} · {entry.eventType} · {new Date(entry.createdAt).toLocaleTimeString()}</span><small>{entry.details}</small></div>)}</div> : <div className="table-empty">Auction actions will be recorded here.</div>}
          </div>

          <div className="panel direct-bid-panel">
            <div className="panel-heading"><div><h3>Next valid bid</h3><p>Captains submit from their assigned team desk</p></div><Gavel size={17} /></div>
            <strong className="current-price">{formatMoney(auction.nextBidAmount, auction.currencyCode)}</strong>
            <p>Minimum increment {formatMoney(auction.bidIncrement, auction.currencyCode)} · {auction.bidDurationSeconds}s timer · {auction.bidExtensionSeconds}s late-bid extension</p>
            <Link to={`/auction/bid/${auctionId}`} className="button button-primary">Open captain desk</Link>
          </div>
        </aside>
      </div>
    </main>
  );
}
