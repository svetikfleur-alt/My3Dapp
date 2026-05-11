

  const chatkit = document.getElementById('my-chat') as any;

chatkit.setOptions({
  api: {
    async getClientSecret(currentClientSecret: string | null) {
      if (!currentClientSecret) {
        const res = await fetch('/api/chatkit/start', { method: 'POST' });
        const { client_secret } = await res.json();
        return client_secret;
      }
      const res = await fetch('/api/chatkit/refresh', {
        method: 'POST',
        body: JSON.stringify({ currentClientSecret }),
        headers: {
          'Content-Type': 'application/json',
        },
      });
      const { client_secret } = await res.json();
      return client_secret;
    },
  },
});
