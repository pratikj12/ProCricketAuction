import { useEffect, useRef, useState } from 'react';
import { Maximize, Radio, ShieldCheck, Trophy } from 'lucide-react';
import { useParams } from 'react-router-dom';
import { formatMoney } from '../context/AuctionDataContext.jsx';
import useAuction from '../hooks/useAuction.js';

function initials(name = '') {
  return name.split(' ').map((part) => part[0]).slice(0, 2).join('');
}

export default function AuctionDisplay({ publicView = false }) {
  const { auctionId } = useParams();
  const { auction, loading, error, connected, remainingSeconds } = useAuction(auctionId, { publicView });
  const previousState = useRef(null);
  const [resultBanner, setResultBanner] = useState(null);
  const player = auction?.currentPlayer;
  const leader = auction?.teams.find((team) => team.id === auction.leadingTeamId);
  const recentSales = (auction?.recentSales ?? []).filter((sale) => !sale.isReversed).slice(0, 4);

  useEffect(() => {
    if (!auction) return undefined;
    const previous = previousState.current;
    if (previous?.currentPlayer && previous.currentPlayer.id !== auction.currentPlayer?.id) {
      const finalized = auction.players.find((item) => item.id === previous.currentPlayer.id);
      if (finalized?.status === 'Sold' || finalized?.status === 'Unsold') {
        setResultBanner({ status: finalized.status, playerName: finalized.name, teamName: finalized.status === 'Sold'
          ? auction.teams.find((team) => team.id === finalized.teamId)?.name
          : null });
        const timeout = window.setTimeout(() => setResultBanner(null), 4500);
        previousState.current = auction;
        return () => window.clearTimeout(timeout);
      }
    }
    previousState.current = auction;
    return undefined;
  }, [auction]);

  const fullscreen = () => document.documentElement.requestFullscreen?.();
  if (loading && !auction) return <div className="display-screen"><section className="display-waiting">Connecting to the auction…</section></div>;
  if (!auction) return <div className="display-screen"><section className="display-waiting" role="alert">{error || 'This auction is not available.'}</section></div>;

  return (
    <div className="display-screen">
      <header className="display-header">
        <div className="display-brand"><span className="display-brand-mark">P</span><span>PROCRICKET<small>LIVE AUCTION</small></span></div>
        <div className="display-tournament"><span>{auction.seasonName} · {auction.auctionName}</span><b>{auction.tournamentName}</b></div>
        <div className="display-live"><i /> {connected ? auction.status === 'Live' ? 'LIVE NOW' : auction.status.toUpperCase() : 'RECONNECTING'} <button onClick={fullscreen} aria-label="Full screen"><Maximize size={17} /></button></div>
      </header>

      <div className="display-content">
        {player ? <>
          <section className="display-player-card">
            <div className="display-player-art">
              <div className="display-art-orbit" />
              {player.photoDataUrl ? <img src={player.photoDataUrl} alt={player.name} /> : <span>{initials(player.name)}</span>}
              <small>{player.role.toUpperCase()}</small>
            </div>
            <div className="display-player-details">
              <div className="eyebrow"><span className="eyebrow-line" />NOW ON THE BLOCK <span className="display-lot">{player.registrationNumber}</span></div>
              <h1>{player.name}</h1><p>{player.city || 'Registered player'} <i /> {player.role}</p>
              <div className="display-price-row"><div><small>BASE PRICE</small><b>{formatMoney(player.basePrice, auction.currencyCode)}</b></div><div className="display-price-divider" /><div><small>CURRENT BID</small><b className="display-gold">{formatMoney(auction.currentBidAmount, auction.currencyCode)}</b></div></div>
            </div>
            <div className="display-timer"><span>TIME REMAINING</span><b className={remainingSeconds < 10 ? 'timer-low' : ''}>00:{String(remainingSeconds).padStart(2, '0')}</b><div className="display-timer-track"><i style={{ width: `${auction.bidDurationSeconds ? remainingSeconds / auction.bidDurationSeconds * 100 : 0}%` }} /></div></div>
          </section>
          <section className={`display-leader ${leader ? '' : 'no-leader'}`}><div className="leader-crown">♛</div><div><small>{leader ? 'CURRENTLY LEADING THE BIDDING' : 'BIDDING IS OPEN'}</small><b>{leader?.name || 'Who wants to start it?'}</b></div><strong>{formatMoney(auction.currentBidAmount || player.basePrice, auction.currencyCode)}</strong><div className="highest-bid-label"><Trophy size={14} /> {leader ? 'HIGHEST BID' : 'BASE PRICE'}</div></section>
        </> : <section className="display-waiting">
          <div className="waiting-mark"><Radio size={30} /></div>
          <span>{auction.status === 'Completed' ? 'AUCTION ROUND COMPLETE' : 'WAITING FOR THE NEXT PLAYER'}</span>
          <h1>Big moments.<br /><em>Bigger dreams.</em></h1><p>{auction.status === 'Completed' ? 'The auctioneer will open another round if players remain unsold.' : 'The next eligible player will appear here as soon as the auctioneer presents them.'}</p>
        </section>}

        {resultBanner && <section className={`display-result-banner ${resultBanner.status.toLowerCase()}`} aria-live="polite">
          <strong>{resultBanner.status === 'Sold' ? 'SOLD' : 'UNSOLD'}</strong><span>{resultBanner.playerName}{resultBanner.teamName ? ` · ${resultBanner.teamName}` : ''}</span>
        </section>}

        <section className="display-teams-section">
          <div className="display-section-heading"><div><span className="eyebrow"><span className="eyebrow-line" />THE FRANCHISES</span><h2>Live purse overview.</h2></div><span><i /> SERVER SYNCED</span></div>
          <div className="display-team-grid">{auction.teams.map((team, index) => <article className={`display-team-card ${team.id === auction.leadingTeamId ? 'display-leading' : ''}`} key={team.id}>
            <div className={`team-mark team-mark-${index + 1}`} style={{ '--team': team.color }}>{team.shortName}</div>
            <div><span>{team.name}</span><b>{formatMoney(team.purseRemaining, auction.currencyCode)}</b><small>{team.playersPurchased} bought · {team.squadSlotsRemaining} slots left</small></div>
            {team.id === auction.leadingTeamId && <small>LEADING</small>}
          </article>)}</div>
        </section>
      </div>

      <footer className="display-footer"><span><ShieldCheck size={14} /> OFFICIAL AUCTION DISPLAY</span><span><i /> {connected ? 'Updates synchronized with the server' : 'Connection recovery in progress'}</span><div className="display-brand mini"><span className="display-brand-mark">P</span>PROCRICKET</div></footer>
      {recentSales.length > 0 && <div className="display-recent"><span>RECENTLY SOLD</span>{recentSales.map((sale) => <b key={sale.id}>{sale.playerName} <i>·</i> {formatMoney(sale.price, auction.currencyCode)}</b>)}</div>}
    </div>
  );
}
