import { useState } from 'react';
import { ArrowRight, Command, ShieldCheck } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext.jsx';

function errorMessage(error) {
  const data = error.response?.data;
  return data?.detail || data?.title || Object.values(data?.errors ?? {}).flat()[0] || 'Unable to sign in. Check your connection and try again.';
}

export default function Login({ serverError }) {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [credentials, setCredentials] = useState({ email: '', password: '' });
  const [submitting, setSubmitting] = useState(false);

  async function submit(event) {
    event.preventDefault();
    setSubmitting(true);
    try {
      await login({ email: credentials.email.trim(), password: credentials.password });
      toast.success('You’re signed in.');
      navigate('/dashboard', { replace: true });
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setSubmitting(false);
    }
  }

  return <main className="login-page"><Link to="/dashboard" className="login-brand"><span><Command size={19} /></span>ProCricket<span>.</span></Link><section className="login-card"><div className="login-icon"><ShieldCheck size={22} /></div><div className="eyebrow"><span className="eyebrow-line" />ORGANIZER ACCESS</div><h1>Welcome back.</h1><p>Sign in to manage your tournament, teams, and live auction.</p>{serverError && <div className="auth-server-error">{serverError}</div>}<form onSubmit={submit}><label>Email address<input autoComplete="username" type="email" required maxLength={256} value={credentials.email} onChange={(event) => setCredentials({ ...credentials, email: event.target.value })} placeholder="you@yourclub.com" /></label><label>Password<input autoComplete="current-password" type="password" required value={credentials.password} onChange={(event) => setCredentials({ ...credentials, password: event.target.value })} placeholder="Your password" /></label><button className="button button-primary login-button" type="submit" disabled={submitting}>{submitting ? 'Signing in…' : 'Sign in'} <ArrowRight size={16} /></button></form><div className="login-disclaimer"><ShieldCheck size={15} /> Your password is checked by the secure account server.</div><div className="auth-switch">New to ProCricket Auction? <Link to="/register">Create an organizer account</Link></div></section><div className="login-footer">BUILT FOR THE LOVE OF THE GAME <span>·</span> PROCRICKET AUCTION</div></main>;
}
