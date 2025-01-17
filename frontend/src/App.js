import React, { useState, useEffect, useCallback } from 'react';
import { taskApi, workerApi } from './services/api';
import { useSignalR } from './hooks/useSignalR';
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, LineChart, Line
} from 'recharts';
import { formatDistanceToNow } from 'date-fns';

const PRIORITY_COLORS = { Critical: '#ef4444', High: '#f97316', Normal: '#3b82f6', Low: '#6b7280' };
const STATUS_COLORS = {
  Queued: '#f59e0b', Processing: '#3b82f6', Completed: '#10b981',
  Failed: '#ef4444', Cancelled: '#6b7280', TimedOut: '#8b5cf6'
};

function StatusBadge({ status }) {
  return (
    <span style={{
      backgroundColor: STATUS_COLORS[status] + '22',
      color: STATUS_COLORS[status],
      border: `1px solid ${STATUS_COLORS[status]}44`,
      padding: '2px 8px', borderRadius: 12, fontSize: 12, fontWeight: 600
    }}>
      {status}
    </span>
  );
}

function StatCard({ label, value, sub, color = '#3b82f6' }) {
  return (
    <div style={{ background: '#1e293b', borderRadius: 12, padding: '20px 24px', flex: 1, minWidth: 140, border: '1px solid #334155' }}>
      <div style={{ color: '#94a3b8', fontSize: 13, marginBottom: 6 }}>{label}</div>
      <div style={{ color, fontSize: 32, fontWeight: 700 }}>{value ?? '—'}</div>
      {sub && <div style={{ color: '#64748b', fontSize: 12, marginTop: 4 }}>{sub}</div>}
    </div>
  );
}

function SubmitTaskForm({ onSubmit }) {
  const [form, setForm] = useState({
    type: 'email', priority: 'Normal', name: '', payload: '{"to":"user@example.com","subject":"Hello"}', maxRetries: 3
  });
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);

  const taskTypes = ['email', 'data-processing', 'report-generation', 'webhook', 'image-resize'];
  const priorities = ['Low', 'Normal', 'High', 'Critical'];

  const defaultPayloads = {
    email: '{"to":"user@example.com","subject":"Test Email"}',
    'data-processing': '{"record_count":5000,"source":"db"}',
    'report-generation': '{"report_type":"pdf","date_range":"last-30-days"}',
    webhook: '{"url":"https://webhook.site/test","event_type":"user.signup"}',
    'image-resize': '{"width":1200,"height":630,"format":"webp","source_url":"https://example.com/img.jpg"}'
  };

  const handleTypeChange = (type) => {
    setForm(f => ({ ...f, type, payload: defaultPayloads[type] || '{}' }));
  };

  const handleSubmit = async () => {
    setLoading(true);
    setResult(null);
    try {
      let payload = {};
      try { payload = JSON.parse(form.payload); } catch { payload = {}; }
      const res = await taskApi.submitTask({
        type: form.type, name: form.name || form.type, priority: form.priority,
        payload, maxRetries: form.maxRetries
      });
      setResult({ success: true, taskId: res.taskId });
      onSubmit();
    } catch (err) {
      setResult({ success: false, error: err.message });
    } finally { setLoading(false); }
  };

  const inp = (label, value, onChange, type = 'text') => (
    <div style={{ marginBottom: 14 }}>
      <label style={{ display: 'block', color: '#94a3b8', fontSize: 12, marginBottom: 4 }}>{label}</label>
      <input type={type} value={value} onChange={e => onChange(e.target.value)}
        style={{ width: '100%', background: '#0f172a', border: '1px solid #334155', borderRadius: 6, padding: '8px 10px', color: '#e2e8f0', fontSize: 13, boxSizing: 'border-box' }} />
    </div>
  );

  return (
    <div style={{ background: '#1e293b', borderRadius: 12, padding: 24, border: '1px solid #334155' }}>
      <h3 style={{ color: '#e2e8f0', margin: '0 0 20px', fontSize: 16 }}>Submit Task</h3>
      <div style={{ marginBottom: 14 }}>
        <label style={{ display: 'block', color: '#94a3b8', fontSize: 12, marginBottom: 4 }}>Task Type</label>
        <select value={form.type} onChange={e => handleTypeChange(e.target.value)}
          style={{ width: '100%', background: '#0f172a', border: '1px solid #334155', borderRadius: 6, padding: '8px 10px', color: '#e2e8f0', fontSize: 13 }}>
          {taskTypes.map(t => <option key={t} value={t}>{t}</option>)}
        </select>
      </div>
      {inp('Name (optional)', form.name, v => setForm(f => ({ ...f, name: v })))}
      <div style={{ marginBottom: 14 }}>
        <label style={{ display: 'block', color: '#94a3b8', fontSize: 12, marginBottom: 4 }}>Priority</label>
        <div style={{ display: 'flex', gap: 8 }}>
          {priorities.map(p => (
            <button key={p} onClick={() => setForm(f => ({ ...f, priority: p }))}
              style={{
                flex: 1, padding: '6px 0', borderRadius: 6, border: '1px solid',
                borderColor: form.priority === p ? PRIORITY_COLORS[p] : '#334155',
                background: form.priority === p ? PRIORITY_COLORS[p] + '22' : 'transparent',
                color: form.priority === p ? PRIORITY_COLORS[p] : '#64748b',
                fontSize: 12, cursor: 'pointer', fontWeight: 600
              }}>{p}</button>
          ))}
        </div>
      </div>
      <div style={{ marginBottom: 14 }}>
        <label style={{ display: 'block', color: '#94a3b8', fontSize: 12, marginBottom: 4 }}>Payload (JSON)</label>
        <textarea value={form.payload} onChange={e => setForm(f => ({ ...f, payload: e.target.value }))} rows={3}
          style={{ width: '100%', background: '#0f172a', border: '1px solid #334155', borderRadius: 6, padding: '8px 10px', color: '#e2e8f0', fontSize: 12, fontFamily: 'monospace', resize: 'vertical', boxSizing: 'border-box' }} />
      </div>
      <button onClick={handleSubmit} disabled={loading}
        style={{ width: '100%', padding: '10px 0', background: loading ? '#334155' : '#3b82f6', color: '#fff', border: 'none', borderRadius: 8, fontSize: 14, fontWeight: 600, cursor: loading ? 'not-allowed' : 'pointer' }}>
        {loading ? 'Submitting...' : 'Submit Task'}
      </button>
      {result && (
        <div style={{ marginTop: 12, padding: '10px 14px', borderRadius: 6, background: result.success ? '#10b98122' : '#ef444422', color: result.success ? '#10b981' : '#ef4444', fontSize: 13 }}>
          {result.success ? `✓ Task queued: ${result.taskId}` : `✗ Error: ${result.error}`}
        </div>
      )}
    </div>
  );
}

function WorkersPanel({ workers }) {
  return (
    <div style={{ background: '#1e293b', borderRadius: 12, padding: 24, border: '1px solid #334155' }}>
      <h3 style={{ color: '#e2e8f0', margin: '0 0 16px', fontSize: 16 }}>
        Workers <span style={{ color: '#64748b', fontWeight: 400, fontSize: 13 }}>({workers.length} active)</span>
      </h3>
      {workers.length === 0 ? (
        <div style={{ color: '#475569', fontSize: 13, textAlign: 'center', padding: '20px 0' }}>No active workers. Start a worker instance.</div>
      ) : (
        workers.map(w => (
          <div key={w.workerId} style={{ background: '#0f172a', borderRadius: 8, padding: '12px 14px', marginBottom: 10, border: '1px solid #1e293b' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
              <span style={{ color: '#e2e8f0', fontSize: 13, fontWeight: 600 }}>{w.workerId}</span>
              <span style={{
                fontSize: 11, padding: '2px 8px', borderRadius: 10, fontWeight: 600,
                background: w.status === 'Busy' ? '#f97316' + '22' : '#10b981' + '22',
                color: w.status === 'Busy' ? '#f97316' : '#10b981'
              }}>{w.status}</span>
            </div>
            <div style={{ color: '#475569', fontSize: 12 }}>Host: {w.hostName}</div>
            {w.currentTaskId && <div style={{ color: '#64748b', fontSize: 11, marginTop: 4 }}>Task: {w.currentTaskId}</div>}
            <div style={{ display: 'flex', gap: 12, marginTop: 6 }}>
              <span style={{ color: '#64748b', fontSize: 11 }}>✓ {w.tasksProcessed}</span>
              <span style={{ color: '#64748b', fontSize: 11 }}>✗ {w.tasksFailed}</span>
              <span style={{ color: '#64748b', fontSize: 11 }}>CPU {w.cpuUsage?.toFixed(1)}%</span>
            </div>
          </div>
        ))
      )}
    </div>
  );
}

export default function App() {
  const [tasks, setTasks] = useState([]);
  const [workers, setWorkers] = useState([]);
  const [statsHistory, setStatsHistory] = useState([]);
  const [activeTab, setActiveTab] = useState('dashboard');
  const { connected, stats, events } = useSignalR();

  const fetchData = useCallback(async () => {
    try {
      const [t, w] = await Promise.all([taskApi.getRecentTasks(30), workerApi.getWorkers()]);
      setTasks(t);
      setWorkers(w);
    } catch (err) { /* polling fallback */ }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  // Poll if not connected to SignalR
  useEffect(() => {
    if (!connected) {
      const interval = setInterval(fetchData, 3000);
      return () => clearInterval(interval);
    }
  }, [connected, fetchData]);

  // Build stats history for chart
  useEffect(() => {
    if (stats) {
      setStatsHistory(prev => [...prev.slice(-19), { ...stats, time: new Date().toLocaleTimeString() }]);
    }
  }, [stats]);

  const tabs = ['dashboard', 'tasks', 'workers', 'submit'];

  return (
    <div style={{ minHeight: '100vh', background: '#0f172a', color: '#e2e8f0', fontFamily: 'system-ui, -apple-system, sans-serif' }}>
      {/* Header */}
      <div style={{ borderBottom: '1px solid #1e293b', padding: '16px 32px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 20, fontWeight: 700, color: '#f8fafc' }}>⚡ Distributed Task Engine</h1>
          <div style={{ fontSize: 12, color: '#475569', marginTop: 2 }}>Real-time task orchestration dashboard</div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <div style={{ width: 8, height: 8, borderRadius: '50%', background: connected ? '#10b981' : '#ef4444' }} />
          <span style={{ fontSize: 12, color: '#64748b' }}>{connected ? 'Live' : 'Polling'}</span>
        </div>
      </div>

      {/* Nav */}
      <div style={{ display: 'flex', gap: 4, padding: '12px 32px', borderBottom: '1px solid #1e293b' }}>
        {tabs.map(tab => (
          <button key={tab} onClick={() => setActiveTab(tab)}
            style={{
              padding: '6px 16px', borderRadius: 6, border: 'none', cursor: 'pointer', fontSize: 13, fontWeight: 500, textTransform: 'capitalize',
              background: activeTab === tab ? '#3b82f6' : 'transparent',
              color: activeTab === tab ? '#fff' : '#64748b'
            }}>{tab}</button>
        ))}
      </div>

      <div style={{ padding: '24px 32px' }}>
        {activeTab === 'dashboard' && (
          <>
            {/* Stats row */}
            <div style={{ display: 'flex', gap: 16, marginBottom: 24, flexWrap: 'wrap' }}>
              <StatCard label="Pending" value={stats?.pendingTasks ?? 0} color="#f59e0b" />
              <StatCard label="Processing" value={stats?.processingTasks ?? 0} color="#3b82f6" />
              <StatCard label="Completed" value={stats?.completedTasks ?? 0} color="#10b981" />
              <StatCard label="Failed" value={stats?.failedTasks ?? 0} color="#ef4444" />
              <StatCard label="Workers" value={stats?.activeWorkers ?? 0} color="#8b5cf6" />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 24 }}>
              {/* Live throughput chart */}
              <div style={{ background: '#1e293b', borderRadius: 12, padding: 24, border: '1px solid #334155' }}>
                <h3 style={{ color: '#e2e8f0', margin: '0 0 16px', fontSize: 15 }}>Queue Depth Over Time</h3>
                <ResponsiveContainer width="100%" height={200}>
                  <LineChart data={statsHistory}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                    <XAxis dataKey="time" tick={{ fill: '#64748b', fontSize: 10 }} />
                    <YAxis tick={{ fill: '#64748b', fontSize: 10 }} />
                    <Tooltip contentStyle={{ background: '#1e293b', border: '1px solid #334155', borderRadius: 8, color: '#e2e8f0' }} />
                    <Line type="monotone" dataKey="pendingTasks" stroke="#f59e0b" dot={false} name="Pending" />
                    <Line type="monotone" dataKey="processingTasks" stroke="#3b82f6" dot={false} name="Processing" />
                  </LineChart>
                </ResponsiveContainer>
              </div>

              {/* Priority breakdown */}
              <div style={{ background: '#1e293b', borderRadius: 12, padding: 24, border: '1px solid #334155' }}>
                <h3 style={{ color: '#e2e8f0', margin: '0 0 16px', fontSize: 15 }}>Tasks by Priority</h3>
                <ResponsiveContainer width="100%" height={200}>
                  <BarChart data={stats?.tasksByPriority ? Object.entries(stats.tasksByPriority).map(([k, v]) => ({ name: k, count: v })) : []}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
                    <XAxis dataKey="name" tick={{ fill: '#64748b', fontSize: 11 }} />
                    <YAxis tick={{ fill: '#64748b', fontSize: 11 }} />
                    <Tooltip contentStyle={{ background: '#1e293b', border: '1px solid #334155', borderRadius: 8, color: '#e2e8f0' }} />
                    <Bar dataKey="count" fill="#3b82f6" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>

              {/* Live event log */}
              <div style={{ background: '#1e293b', borderRadius: 12, padding: 24, border: '1px solid #334155', gridColumn: '1 / -1' }}>
                <h3 style={{ color: '#e2e8f0', margin: '0 0 12px', fontSize: 15 }}>Live Event Stream</h3>
                <div style={{ fontFamily: 'monospace', fontSize: 12, maxHeight: 200, overflowY: 'auto' }}>
                  {events.length === 0 ? (
                    <div style={{ color: '#475569', padding: '12px 0' }}>No events yet. Submit a task to see real-time updates.</div>
                  ) : events.map((e, i) => (
                    <div key={i} style={{ display: 'flex', gap: 12, padding: '4px 0', borderBottom: '1px solid #0f172a' }}>
                      <span style={{ color: '#475569', minWidth: 80 }}>{e.receivedAt?.toLocaleTimeString()}</span>
                      <span style={{ color: '#3b82f6', minWidth: 120 }}>{e.eventType}</span>
                      <span style={{ color: '#94a3b8' }}>{e.taskId?.substring(0, 16)}...</span>
                      <span style={{ color: '#64748b' }}>{e.taskType}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </>
        )}

        {activeTab === 'tasks' && (
          <div style={{ background: '#1e293b', borderRadius: 12, border: '1px solid #334155', overflow: 'hidden' }}>
            <div style={{ padding: '16px 20px', borderBottom: '1px solid #334155', display: 'flex', justifyContent: 'space-between' }}>
              <h3 style={{ margin: 0, fontSize: 15, color: '#e2e8f0' }}>Recent Tasks</h3>
              <button onClick={fetchData} style={{ padding: '5px 12px', background: '#334155', border: 'none', borderRadius: 6, color: '#94a3b8', cursor: 'pointer', fontSize: 12 }}>Refresh</button>
            </div>
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead>
                  <tr style={{ background: '#0f172a' }}>
                    {['ID', 'Name', 'Type', 'Priority', 'Status', 'Created', 'Duration'].map(h => (
                      <th key={h} style={{ padding: '10px 16px', textAlign: 'left', color: '#64748b', fontSize: 12, fontWeight: 600, borderBottom: '1px solid #334155' }}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {tasks.map(t => (
                    <tr key={t.id} style={{ borderBottom: '1px solid #1e293b' }}>
                      <td style={{ padding: '10px 16px', color: '#64748b', fontSize: 12, fontFamily: 'monospace' }}>{t.id?.substring(0, 12)}...</td>
                      <td style={{ padding: '10px 16px', color: '#e2e8f0', fontSize: 13 }}>{t.name}</td>
                      <td style={{ padding: '10px 16px', color: '#94a3b8', fontSize: 12 }}>{t.type}</td>
                      <td style={{ padding: '10px 16px' }}>
                        <span style={{ color: PRIORITY_COLORS[t.priority] || '#94a3b8', fontSize: 12, fontWeight: 600 }}>{t.priority}</span>
                      </td>
                      <td style={{ padding: '10px 16px' }}><StatusBadge status={t.status} /></td>
                      <td style={{ padding: '10px 16px', color: '#64748b', fontSize: 12 }}>
                        {t.createdAt ? formatDistanceToNow(new Date(t.createdAt), { addSuffix: true }) : '—'}
                      </td>
                      <td style={{ padding: '10px 16px', color: '#64748b', fontSize: 12 }}>
                        {t.startedAt && t.completedAt
                          ? `${Math.round((new Date(t.completedAt) - new Date(t.startedAt)) / 1000)}s`
                          : t.status === 'Processing' ? '⏳' : '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {activeTab === 'workers' && <WorkersPanel workers={workers} />}

        {activeTab === 'submit' && (
          <div style={{ maxWidth: 520 }}>
            <SubmitTaskForm onSubmit={fetchData} />
          </div>
        )}
      </div>
    </div>
  );
}
