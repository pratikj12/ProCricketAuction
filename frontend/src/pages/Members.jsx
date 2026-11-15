import { useEffect, useState } from 'react';
import { UserPlus, Users, UserX } from 'lucide-react';
import toast from 'react-hot-toast';
import { useAuctionData } from '../context/AuctionDataContext.jsx';
import tournamentService from '../services/tournamentService.js';

const roles = ['Administrator', 'Auctioneer', 'Captain', 'Viewer'];

export default function Members() {
  const { tournament, teams } = useAuctionData();
  const [members, setMembers] = useState([]);
  const [email, setEmail] = useState('');
  const [role, setRole] = useState('Auctioneer');
  const [assignedTeamId, setAssignedTeamId] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    if (!tournament.id) {
      setMembers([]);
      return () => { active = false; };
    }
    setLoading(true);
    setError('');
    tournamentService.listMembers(tournament.id)
      .then((data) => { if (active) setMembers(data); })
      .catch((requestError) => {
        if (active) setError(requestError.response?.data?.detail || 'Could not load league members. Only league owners and administrators can manage access.');
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [tournament.id]);

  async function addMember(event) {
    event.preventDefault();
    try {
      const created = await tournamentService.addMember(tournament.id, {
        email: email.trim(),
        role,
        teamIds: role === 'Captain' && assignedTeamId ? [assignedTeamId] : []
      });
      setMembers((current) => [...current.filter((member) => member.userId !== created.userId), created]);
      setEmail('');
      setAssignedTeamId('');
      toast.success(`${created.displayName} added as ${created.role}.`);
    } catch (requestError) {
      toast.error(requestError.response?.data?.detail || requestError.response?.data?.title || 'Could not add this member.');
    }
  }

  async function changeRole(member, nextRole) {
    try {
      const updated = await tournamentService.updateMemberRole(
        tournament.id, member.userId, nextRole, nextRole === 'Captain' ? member.teamIds : []);
      setMembers((current) => current.map((item) => item.userId === updated.userId ? updated : item));
      toast.success(`${member.displayName}'s role updated.`);
    } catch (requestError) {
      toast.error(requestError.response?.data?.detail || 'Could not update this role.');
    }

    async function changeAssignedTeam(member, nextTeamId) {
      const currentSeasonTeams = new Set(teams.map((team) => team.id));
      const otherSeasonAssignments = member.teamIds.filter((teamId) => !currentSeasonTeams.has(teamId));
      const teamIds = nextTeamId ? [...otherSeasonAssignments, nextTeamId] : otherSeasonAssignments;
      try {
        const updated = await tournamentService.updateMemberRole(
          tournament.id, member.userId, member.role, teamIds);
        setMembers((current) => current.map((item) => item.userId === updated.userId ? updated : item));
        toast.success(`${member.displayName}'s captain team assignment updated.`);
      } catch (requestError) {
        toast.error(requestError.response?.data?.detail || 'Could not update this team assignment.');
      }
    }
  }

  async function removeMember(member) {
    if (!window.confirm(`Remove ${member.displayName} from ${tournament.name}?`)) return;
    try {
      await tournamentService.removeMember(tournament.id, member.userId);
      setMembers((current) => current.filter((item) => item.userId !== member.userId));
      toast.success(`${member.displayName} removed from the league.`);
    } catch (requestError) {
      toast.error(requestError.response?.data?.detail || 'Could not remove this member.');
    }
  }

  return (
    <main>
      <div className="page-title-row"><div><div className="eyebrow"><span className="eyebrow-line" />LEAGUE ACCESS</div><h1>Members & roles</h1><p className="subheading">{tournament.name ? `Manage people assigned to ${tournament.name}.` : 'Create a league before managing members.'}</p></div></div>
      {!tournament.id ? <section className="panel table-empty">Create or select a league to manage its members.</section>
        : error ? <section className="panel workspace-error" role="alert">{error}</section>
          : <div className="members-layout">
            <section className="panel member-invite-panel">
              <div className="panel-heading"><div><h3>Add a member</h3><p>Assign a role to a registered ProCricket account.</p></div><UserPlus size={19} /></div>
              <form onSubmit={addMember}>
                <label>Account email<input required type="email" maxLength="254" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="captain@example.com" /></label>
                <label>League role<select value={role} onChange={(event) => {
                  setRole(event.target.value);
                  if (event.target.value !== 'Captain') setAssignedTeamId('');
                }}>{roles.map((option) => <option key={option}>{option}</option>)}</select></label>
                {role === 'Captain' && <label>Assigned team<select required value={assignedTeamId} onChange={(event) => setAssignedTeamId(event.target.value)}><option value="">Choose a team</option>{teams.map((team) => <option key={team.id} value={team.id}>{team.name} · {team.shortName}</option>)}</select></label>}
                <button className="button button-primary" type="submit" disabled={loading}><UserPlus size={15} /> Add member</button>
              </form>
              <p className="member-invite-note">Only existing registered accounts can be added. This app does not send invitation emails or create pending invitations.</p>
            </section>
            <section className="panel member-list-panel">
              <div className="panel-heading"><div><h3>League members</h3><p>People currently assigned to this league.</p></div><span className="member-count"><Users size={15} /> {members.length}</span></div>
              {loading ? <div className="table-empty">Loading members…</div>
                : members.length ? <div className="member-list">{members.map((member) => (
                  <article className="member-row" key={member.userId}>
                    <div className="member-avatar">{member.displayName.split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase()}</div>
                    <div className="member-details"><b>{member.displayName}</b><small>{member.email}</small></div>
                    {member.role === 'Owner' ? <span className="member-owner-badge">Owner</span>
                      : <><select aria-label={`Role for ${member.displayName}`} value={member.role} onChange={(event) => changeRole(member, event.target.value)}>{roles.map((option) => <option key={option}>{option}</option>)}</select>{member.role === 'Captain' && <select aria-label={`Team assignment for ${member.displayName}`} value={member.teamIds.find((id) => teams.some((team) => team.id === id)) ?? ''} onChange={(event) => changeAssignedTeam(member, event.target.value)}><option value="">Choose team</option>{teams.map((team) => <option key={team.id} value={team.id}>{team.name} · {team.shortName}</option>)}</select>}<button className="icon-button" type="button" onClick={() => removeMember(member)} aria-label={`Remove ${member.displayName}`}><UserX size={16} /></button></>}
                  </article>
                ))}</div> : <div className="table-empty">No members are assigned to this league yet.</div>}
            </section>
          </div>}
    </main>
  );
}
