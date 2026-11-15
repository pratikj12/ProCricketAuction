import { ArrowRight, BarChart3, Download, Printer, Users, Wallet } from 'lucide-react';
import { Link } from 'react-router-dom';
import { formatMoney, useAuctionData } from '../context/AuctionDataContext.jsx';

function downloadCsv(filename, rows) {
  const csv = rows.map((row) => row.map((cell) => `"${String(cell ?? '').replaceAll('"', '""')}"`).join(',')).join('\r\n');
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

export default function Reports() {
  const { players, teams, tournament } = useAuctionData();
  const sold = players.filter((player) => player.status === 'Sold');
  const unsold = players.filter((player) => player.status === 'Unsold');
  const available = players.filter((player) => player.status === 'Available');
  const totalSpend = sold.reduce((sum, player) => sum + (player.soldPrice || 0), 0);
  const teamSpend = teams.map((team) => {
    const purchases = sold.filter((player) => player.teamId === team.id);
    const spend = purchases.reduce((sum, player) => sum + (player.soldPrice || 0), 0);
    return { team, spend, squadSize: purchases.length, remaining: team.purse ?? 0 };
  });
  const maxSpend = Math.max(1, ...teamSpend.map((entry) => entry.spend));
  const initialPurseTotal = teams.reduce((sum, team) => {
    const spend = teamSpend.find((entry) => entry.team.id === team.id)?.spend ?? 0;
    return sum + (team.initialPurse ?? team.purse + spend);
  }, 0);
  const averageSale = sold.length ? Math.round(totalSpend / sold.length) : 0;

  function exportReport() {
    downloadCsv('auction-team-report.csv', [
      ['Tournament', tournament.name, 'Season', tournament.season, 'Currency', tournament.currencyCode],
      [],
      ['Team', 'Players bought', 'Amount spent', 'Purse remaining'],
      ...teamSpend.map(({ team, squadSize, spend, remaining }) => [team.name, squadSize, spend, remaining]),
      [],
      ['Player', 'Role', 'Status', 'Team', 'Base price', 'Sold price'],
      ...players.map((player) => [
        player.name,
        player.role,
        player.status,
        teams.find((team) => team.id === player.teamId)?.name ?? '',
        player.basePrice,
        player.soldPrice ?? ''
      ])
    ]);
  }

  return (
    <main>
      <div className="page-title-row">
        <div><div className="eyebrow"><span className="eyebrow-line" />THE BIG PICTURE</div><h1>Reports & insights</h1><p className="subheading">A clear view of the numbers in this tournament workspace.</p></div>
        <div className="toolbar-actions">
          <button className="button button-ghost" onClick={() => window.print()}><Printer size={16} /> Print report</button>
          <button className="button button-ghost" onClick={exportReport}><Download size={16} /> Export CSV</button>
          <Link to="/auction/events" className="button button-ghost">Auction events <ArrowRight size={15} /></Link>
        </div>
      </div>
      <section className="report-metric-grid">
        <article className="report-metric"><span><Users size={18} /></span><small>PLAYER POOL</small><b>{players.length}</b><em>{sold.length} sold · {available.length} available · {unsold.length} unsold</em></article>
        <article className="report-metric"><span><Wallet size={18} /></span><small>TOTAL SPEND</small><b>{formatMoney(totalSpend, tournament.currencyCode)}</b><em>{sold.length} recorded purchases</em></article>
        <article className="report-metric"><span><BarChart3 size={18} /></span><small>AVERAGE SALE</small><b>{formatMoney(averageSale, tournament.currencyCode)}</b><em>Across sold players</em></article>
      </section>
      <section className="panel spend-panel">
        <div className="panel-heading"><div><h3>Spending by team</h3><p>Recorded player purchases by team.</p></div><span className="report-period">{tournament.season ? `SEASON ${tournament.season}` : 'CURRENT SEASON'}</span></div>
        <div className="spending-list">{teamSpend.map(({ team, spend, squadSize, remaining }, index) => <div className="spending-row" key={team.id}><span className={`team-mark team-mark-${index + 1}`} style={{ '--team': team.color }}>{team.shortName}</span><div className="spending-team-info"><div><b>{team.name}</b><span>{squadSize} players</span></div><div className="spending-track"><i style={{ width: `${spend / maxSpend * 100}%`, background: team.color }} /></div></div><b className="spending-amount">{formatMoney(spend, tournament.currencyCode)}</b><span className="spending-purse">{formatMoney(remaining, tournament.currencyCode)} left</span></div>)}</div>
        <div className="spending-legend"><span><i /> Amount spent</span><span><i /> Starting purse {formatMoney(initialPurseTotal, tournament.currencyCode)}</span></div>
      </section>
      <section className="report-shortcuts"><Link to="/players"><span><Users size={18} /></span><div><b>Player roster</b><small>Explore every registered player and their status.</small></div><ArrowRight size={17} /></Link><Link to="/auction/events"><span><BarChart3 size={18} /></span><div><b>Results & purchases</b><small>Choose an auction event to review its saved results.</small></div><ArrowRight size={17} /></Link></section>
      <p className="report-disclaimer">Tournament, team, player, and confirmed auction purchase records are database-backed.</p>
    </main>
  );
}
