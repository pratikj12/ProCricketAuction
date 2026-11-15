import { HeartHandshake, Trophy, Users } from 'lucide-react';

export default function About() {
  return (
    <main>
      <div className="page-title-row">
        <div>
          <div className="eyebrow"><span className="eyebrow-line" />ABOUT THE PROJECT</div>
          <h1>About ProCricket Auction</h1>
          <p className="subheading">A home for local cricket tournaments and the people who make them happen.</p>
        </div>
      </div>
      <div className="help-grid">
        <article className="panel help-card">
          <span><Trophy size={18} /></span>
          <h2>What is this website?</h2>
          <p>ProCricket Auction is a cricket tournament and player-auction management website. It brings tournament setup, team and player organization, and auction-day tools together in one workspace.</p>
        </article>
        <article className="panel help-card">
          <span><HeartHandshake size={18} /></span>
          <h2>Why was it created?</h2>
          <p>The goal is to make organizing community cricket competitions easier, clearer, and more enjoyable: organizers can prepare their tournament, captains can follow the player auction, and everyone can focus on the game.</p>
        </article>
        <article className="panel help-card">
          <span><Users size={18} /></span>
          <h2>Created by</h2>
          <p><strong>Pratik Jadhav</strong></p>
          <a href="mailto:pratiksj1645@gmail.com">pratiksj1645@gmail.com</a>
        </article>
      </div>
    </main>
  );
}
