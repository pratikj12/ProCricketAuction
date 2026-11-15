import { useState } from 'react';
import { ArrowRight, Command, ShieldCheck } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext.jsx';

function errorMessage(error) {
  const data = error.response?.data;
  return data?.detail || Object.values(data?.errors ?? {}).flat()[0] || 'Unable to create your account. Check your details and try again.';
}

export default function Register({ serverError }) {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [account, setAccount] = useState({ displayName: '', email: '', password: '' });
  const [submitting, setSubmitting] = useState(false);

  async function submit(event) {
    event.preventDefault();
    setSubmitting(true);
    try {
      await register({ ...account, displayName: account.displayName.trim(), email: account.email.trim() });
      toast.success('Your organizer account is ready.');
      navigate('/dashboard', { replace: true });
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setSubmitting(false);
    }
  }

  return <main className="login-page"><Link to="/dashboard" className="login-brand"><span><Command size={19} /></span>ProCricket<span>.</span></Link><section className="login-card"><div className="login-icon"><ShieldCheck size={22} /></div><div className="eyebrow"><span className="eyebrow-line" />START YOUR WORKSPACE</div><h1>Create your account.</h1><p>Sign up to organize tournaments and invite your auction crew.</p>{serverError && <div className="auth-server-error">{serverError}</div>}<form onSubmit={submit}><label>Your name<input autoComplete="name" required minLength={2} maxLength={100} value={account.displayName} onChange={(event) => setAccount({ ...account, displayName: event.target.value })} placeholder="Your full name" /></label><label>Email address<input autoComplete="email" type="email" required maxLength={256} value={account.email} onChange={(event) => setAccount({ ...account, email: event.target.value })} placeholder="you@yourclub.com" /></label><label>Password<input autoComplete="new-password" type="password" required minLength={12} maxLength={128} value={account.password} onChange={(event) => setAccount({ ...account, password: event.target.value })} placeholder="At least 12 characters" /></label><small className="password-help">Use upper and lower case letters, a number, and a symbol.</small><button className="button button-primary login-button" type="submit" disabled={submitting}>{submitting ? 'Creating account…' : 'Create account'} <ArrowRight size={16} /></button></form><div className="login-disclaimer"><ShieldCheck size={15} /> New sign-ups receive organizer access; admin accounts are restricted.</div><div className="auth-switch">Already have an account? <Link to="/login">Sign in</Link></div></section><div className="login-footer">BUILT FOR THE LOVE OF THE GAME <span>·</span> PROCRICKET AUCTION</div></main>;
}
