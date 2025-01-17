import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';

const HUB_URL = process.env.REACT_APP_HUB_URL || 'http://localhost:5000/hubs/tasks';

export function useSignalR() {
  const connectionRef = useRef(null);
  const [connected, setConnected] = useState(false);
  const [stats, setStats] = useState(null);
  const [events, setEvents] = useState([]);

  const connect = useCallback(async () => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('StatsUpdate', (newStats) => {
      setStats(newStats);
    });

    connection.on('TaskEvent', (event) => {
      setEvents(prev => [{ ...event, receivedAt: new Date() }, ...prev].slice(0, 100));
    });

    connection.onreconnected(() => setConnected(true));
    connection.onclose(() => setConnected(false));

    try {
      await connection.start();
      setConnected(true);
      connectionRef.current = connection;
    } catch (err) {
      console.warn('SignalR connection failed, running in polling mode');
    }
  }, []);

  useEffect(() => {
    connect();
    return () => {
      connectionRef.current?.stop();
    };
  }, [connect]);

  return { connected, stats, events };
}
