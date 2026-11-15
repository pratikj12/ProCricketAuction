import { useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

function getHubUrl(publicView) {
  const configuredUrl = import.meta.env.VITE_SIGNALR_HUB_URL;
  if (!publicView) {
    return new URL(configuredUrl || '/hubs/auction', window.location.origin).toString();
  }
  const origin = new URL(configuredUrl || window.location.origin, window.location.origin).origin;
  return new URL('/hubs/public-auction', origin).toString();
}

export default function useSignalR(auctionId, onState, onReconnect, { publicView = false } = {}) {
  const stateCallback = useRef(onState);
  const reconnectCallback = useRef(onReconnect);
  const [connected, setConnected] = useState(false);
  const [connectionError, setConnectionError] = useState('');

  useEffect(() => { stateCallback.current = onState; }, [onState]);
  useEffect(() => { reconnectCallback.current = onReconnect; }, [onReconnect]);

  useEffect(() => {
    if (!auctionId) return undefined;

    let disposed = false;
    let retryTimer;
    const connection = new HubConnectionBuilder()
      .withUrl(getHubUrl(publicView), { withCredentials: !publicView })
      .withAutomaticReconnect([0, 1500, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('AuctionStateChanged', (state) => stateCallback.current?.(state));
    connection.onreconnecting((error) => {
      setConnected(false);
      if (error) setConnectionError(error.message || 'SignalR connection interrupted.');
    });
    connection.onreconnected(async () => {
      try {
        await connection.invoke(publicView ? 'JoinPublicAuction' : 'JoinAuction', auctionId);
        setConnected(true);
        setConnectionError('');
        await reconnectCallback.current?.();
      } catch (error) {
        setConnectionError(error.message || 'Could not resynchronize auction state.');
      }
    });
    connection.onclose((error) => {
      setConnected(false);
      if (error) setConnectionError(error.message || 'SignalR connection closed.');
    });

    const connect = async () => {
      try {
        await connection.start();
        if (disposed) return;
        await connection.invoke(publicView ? 'JoinPublicAuction' : 'JoinAuction', auctionId);
        if (disposed) return;
        setConnected(true);
        setConnectionError('');
      } catch (error) {
        if (disposed) return;
        setConnected(false);
        setConnectionError(error.message || 'Could not connect to the live auction.');
        retryTimer = window.setTimeout(connect, 2500);
      }
    };
    void connect();

    return () => {
      disposed = true;
      window.clearTimeout(retryTimer);
      setConnected(false);
      void connection.stop().catch(() => {});
    };
  }, [auctionId, publicView]);

  return { connected, connectionError };
}
