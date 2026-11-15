import { ArrowRight, CircleHelp, Radio, ShieldCheck, Users } from 'lucide-react';
import { Link } from 'react-router-dom';

export default function Help() {
  return (
    <main>
      <div className="page-title-row">
        <div>
          <div className="eyebrow"><span className="eyebrow-line" />PITCHSIDE SUPPORT</div>
          <h1>Help center</h1>
          <p className="subheading">Quick answers for running your first auction.</p>
        </div>
      </div>
      <div className="help-grid">
        <article className="panel help-card">
          <span><ShieldCheck size={18} /></span>
          <h2>How do accounts work?</h2>
          <p>Create an organizer account with your email and a strong password. Passwords are hashed by ASP.NET Identity and sessions use secure HttpOnly cookies.</p>
        </article>
        <article className="panel help-card">
          <span><Users size={18} /></span>
          <h2>How do I add a player?</h2>
          <p>Open Player pool, choose Add player, and save their registration to the selected tournament season.</p>
          <Link to="/players">Go to player pool <ArrowRight size={14} /></Link>
        </article>
        <article className="panel help-card">
          <span><Radio size={18} /></span>
          <h2>How do I run an auction?</h2>
          <p>Open an auction event, start bidding, and confirm a sale from the auctioneer controls. Accepted bids and completed purchases are saved by the server.</p>
          <Link to="/auction/events">Open auction events <ArrowRight size={14} /></Link>
        </article>
        <article className="panel help-card">
          <span><CircleHelp size={18} /></span>
          <h2>Can captains use another device?</h2>
          <p>Yes. Captains sign in on their device and use their assigned team bidding screen. Auction state synchronizes with connected auction screens.</p>
        </article>
        <article className="panel help-card">
          <span><Radio size={18} /></span>
          <h2>Can I share the auction with viewers?</h2>
          <p>Use Share live auction in the auctioneer room to send a read-only link. Viewers can see the public display but cannot bid or manage tournament data.</p>
        </article>
      </div>
    </main>
  );
}
