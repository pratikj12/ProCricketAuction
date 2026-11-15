import { useState } from 'react';
import { AlertTriangle, Trash2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { useAuctionData } from '../../context/AuctionDataContext.jsx';

export default function DeleteTournamentAction({ variant = 'menu' }) {
  const { tournament, auctionEvents, dispatch, workspaceLoading } = useAuctionData();
  const [confirmOpen, setConfirmOpen] = useState(false);

  function requestDelete() {
    if (!tournament.name) {
      toast.error('There is no tournament to delete.');
      return;
    }
    if (auctionEvents.some((event) => ['Live', 'Paused', 'AwaitingDecision'].includes(event.status))) {
      toast.error('Resolve and complete active auction events before deleting this tournament.');
      return;
    }
    setConfirmOpen(true);
  }

  async function confirmDelete() {
    const deletedName = tournament.name;
    try {
      await dispatch({ type: 'delete-tournament' });
      setConfirmOpen(false);
      toast.success(`${deletedName} and its teams and players were deleted.`);
    } catch (error) {
      toast.error(error.response?.data?.detail || error.message || 'Could not delete the tournament.');
    }
  }

  return (
    <>
      <button
        type="button"
        className={variant === 'menu' ? 'workspace-delete' : 'button button-danger'}
        onClick={requestDelete}
        disabled={!tournament.name || workspaceLoading}
        title={tournament.name ? `Delete ${tournament.name}` : 'No tournament to delete'}
      >
        <Trash2 size={variant === 'menu' ? 15 : 16} />
        Delete tournament
      </button>
      {confirmOpen && (
        <div className="modal-backdrop" onClick={() => setConfirmOpen(false)}>
          <section
            className="modal-card delete-confirm-card"
            role="alertdialog"
            aria-modal="true"
            aria-labelledby="delete-tournament-title"
            aria-describedby="delete-tournament-description"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="delete-confirm-icon"><AlertTriangle size={21} /></div>
            <div className="modal-heading">
              <div>
                <h2 id="delete-tournament-title">Delete tournament?</h2>
                <p id="delete-tournament-description">
                  Delete <strong>{tournament.name}</strong>? Its teams and players saved in the database will also be permanently removed.
                </p>
              </div>
              <button type="button" className="icon-button" onClick={() => setConfirmOpen(false)} aria-label="Close">×</button>
            </div>
            <div className="modal-actions">
              <button type="button" className="button button-ghost" onClick={() => setConfirmOpen(false)}>Cancel</button>
              <button type="button" className="button button-danger" onClick={confirmDelete}><Trash2 size={15} /> Delete tournament</button>
            </div>
          </section>
        </div>
      )}
    </>
  );
}
