const fs = require('fs');

const eventName = process.argv[2] || 'unknown';
let input = '';

process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => input += chunk);
process.stdin.on('end', () => {
  let data = {};
  try {
    data = input.trim() ? JSON.parse(input) : {};
  } catch {}

  const entry = {
    timestamp: new Date().toISOString(),
    event: eventName,
    agent: data.agentType || data.agentId || null,
    tool: data.toolName || null,
    session: data.sessionId || null
  };

  fs.mkdirSync('logs', { recursive: true });
  fs.appendFileSync(
    'logs/ai-hook-events.jsonl',
    JSON.stringify(entry) + '\n'
  );
});
