import axios from 'axios';

const API_BASE = process.env.REACT_APP_API_URL || 'http://localhost:5000/api';

const api = axios.create({ baseURL: API_BASE });

export const taskApi = {
  submitTask: (task) => api.post('/tasks', task).then(r => r.data),
  bulkSubmit: (tasks) => api.post('/tasks/bulk', tasks).then(r => r.data),
  getTask: (taskId) => api.get(`/tasks/${taskId}`).then(r => r.data),
  getRecentTasks: (count = 50) => api.get(`/tasks?count=${count}`).then(r => r.data),
  getStats: () => api.get('/tasks/stats').then(r => r.data),
  cancelTask: (taskId) => api.delete(`/tasks/${taskId}`).then(r => r.data),
  retryTask: (taskId) => api.post(`/tasks/${taskId}/retry`).then(r => r.data),
};

export const workerApi = {
  getWorkers: () => api.get('/workers').then(r => r.data),
};
