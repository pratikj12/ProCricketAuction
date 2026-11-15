import { useParams } from 'react-router-dom';
import { Download, Printer, Trophy } from 'lucide-react';
import { formatMoney } from '../context/AuctionDataContext.jsx';
import useAuction from '../hooks/useAuction.js';

export default function Results() {
  const { auctionId } = useParams();
  const { auction, loading, error } = useAuction(auctionId);
  if (loading && !auction) return <main><section className="panel table-empty">Loading persisted auction results…</section></main>;
  if (!auction) return <main><section className="panel workspace-error" role="alert">{error || 'This auction is unavailable.'}</section></main>;

  const sold = auction.players.filter((player) => player.status === 'Sold')
    .sort((first, second) => (second.soldPrice ?? 0) - (first.soldPrice ?? 0));
  const unsold = auction.players.filter((player) => player.status === 'Unsold');
  const teamFor = (teamId) => auction.teams.find((team) => team.id === teamId);
  const totalSpend = sold.reduce((sum, player) => sum + (player.soldPrice || 0), 0);

  const exportCsv = () => {
    const rows = [
      ['Tournament', auction.tournamentName, 'Season', auction.seasonName, 'Auction', auction.auctionName],
      ['Player', 'Registration', 'Role', 'Status', 'Team', 'Base price', 'Sold price'],
      ...auction.players.map((player) => [
        player.name,
        player.registrationNumber,
        player.role,
        player.status,
        teamFor(player.teamId)?.name ?? '',
        player.basePrice,
        player.soldPrice ?? ''
      ])
    ];
    const csv = rows.map((row) => row.map((cell) => `"${String(cell ?? '').replaceAll('"', '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'auction-results.csv';
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 0);
  };

  return (
    <main>
      <div className="page-title-row">
        <div><div className="eyebrow"><span className="eyebrow-line" />THE FINAL SCORECARD</div><h1>Auction results</h1><p className="subheading">{auction.tournamentName} · {auction.seasonName} · {auction.auctionName}</p></div>
        <div className="toolbar-actions"><button className="button button-ghost" onClick={() => window.print()}><Printer size={16} /> Print</button><button className="button button-ghost" onClick={exportCsv}><Download size={16} /> Export CSV</button></div>
      </div>
      <section className="results-summary"><div><span className="result-stat-icon"><Trophy size={19} /></span><b>{sold.length}</b><small>Players sold</small></div><div><span className="result-stat-icon"><span>{auction.currencyCode}</span></span><b>{formatMoney(totalSpend, auction.currencyCode)}</b><small>Total spend</small></div><div><span className="result-stat-icon"><span>↗</span></span><b>{unsold.length}</b><small>Players unsold</small></div></section>
      <section className="panel table-panel result-table">
        <div className="panel-heading"><div><h3>Sold players</h3><p>Confirmed purchases persisted by the auction server.</p></div><span className="result-count">{sold.length} sold</span></div>
        <div className="table-scroll"><table><thead><tr><th>PLAYER</th><th>ROLE</th><th>WINNING TEAM</th><th>BASE PRICE</th><th>SOLD FOR</th></tr></thead><tbody>{sold.map((player, index) => <tr key={player.id}><td><div className="player-cell"><div className={`player-avatar avatar-${index % 5}`}>{player.name.split(' ').map((part) => part[0]).slice(0, 2).join('')}</div><div><b>{player.name}</b><small>{player.registrationNumber}</small></div></div></td><td><span className="role-pill">{player.role}</span></td><td><span className="table-team"><i style={{ background: teamFor(player.teamId)?.color }} />{teamFor(player.teamId)?.name ?? '—'}</span></td><td>{formatMoney(player.basePrice, auction.currencyCode)}</td><td className="price-cell">{formatMoney(player.soldPrice, auction.currencyCode)}</td></tr>)}</tbody></table></div>
        {!sold.length && <div className="table-empty">No players have been sold in this auction yet.</div>}
      </section>
      <section className="unsold-row"><span className="unsold-mark">↗</span><span><b>{unsold.length} unsold players</b><small>Unsold player results are available for configured later auction rounds.</small></span></section>
      <p className="report-disclaimer">Results are loaded from the persisted auction state and update after committed sales.</p>
    </main>
  );
}
