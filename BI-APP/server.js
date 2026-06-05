import express from 'express';
import { createProxyMiddleware } from 'http-proxy-middleware';
import helmet from 'helmet';
import rateLimit from 'express-rate-limit';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = process.env.PORT || 8080;

// Backend endpoint via environment variables. Defaults to localhost 8001.
const BACKEND_URL = process.env.VITE_BACKEND_URL || process.env.BACKEND_URL || 'http://localhost:8001';

// Apply security headers
app.use(helmet({
    contentSecurityPolicy: false, // Disabled to allow Vue/Echarts inline scripts/styles if necessary. Configure strictly in production.
    crossOriginEmbedderPolicy: false
}));

// Apply rate limiting to all requests
const limiter = rateLimit({
    windowMs: 15 * 60 * 1000, // 15 minutes
    max: 1000, // limit each IP to 1000 requests per windowMs
    standardHeaders: true,
    legacyHeaders: false,
    message: 'Too many requests from this IP, please try again after 15 minutes'
});
app.use(limiter);

// Proxy /api requests to the backend
app.use(
    '/api',
    createProxyMiddleware({
        target: BACKEND_URL,
        changeOrigin: true,
        cookieDomainRewrite: "", // ensure cookies work across domains
        secure: false, // if backend is http
    })
);

// Proxy /assets requests to the backend
app.use(
    '/assets',
    createProxyMiddleware({
        target: BACKEND_URL,
        changeOrigin: true,
        secure: false,
    })
);

// Serve the static frontend build
app.use(express.static(path.join(__dirname, 'dist')));

// SPA fallback: any other route gets index.html
app.get('*', (req, res) => {
    res.sendFile(path.join(__dirname, 'dist', 'index.html'));
});

app.listen(PORT, () => {
    console.log(`Server is running on port ${PORT}`);
    console.log(`Proxying /api and /assets to backend at: ${BACKEND_URL}`);
});
