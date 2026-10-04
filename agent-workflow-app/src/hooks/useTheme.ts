import { useCallback, useEffect, useState } from 'react';

type Theme = 'dark' | 'light';

const BFF_THEME_URL = 'http://localhost:5000/api/settings/theme';

export function useTheme(): [Theme, () => void] {
  const [theme, setTheme] = useState<Theme>(() => {
    const attr = document.documentElement.getAttribute('data-theme');
    return attr === 'light' ? 'light' : 'dark';
  });

  useEffect(() => {
    async function loadSavedTheme() {
      try {
        const res = await fetch(BFF_THEME_URL);
        if (res.ok) {
          const data = await res.json();
          const raw = (data.theme || '').toLowerCase();
          const isLight = raw === 'claro' || raw === 'clara' || raw === 'light';
          const newTheme: Theme = isLight ? 'light' : 'dark';
          setTheme(newTheme);
          document.documentElement.setAttribute('data-theme', newTheme);
        }
      } catch {
        // Fallback local
      }
    }
    loadSavedTheme();
  }, []);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
  }, [theme]);

  const toggle = useCallback(() => {
    setTheme((t) => {
      const nextTheme: Theme = t === 'dark' ? 'light' : 'dark';
      const payloadTheme = nextTheme === 'light' ? 'claro' : 'escuro';

      fetch(BFF_THEME_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ theme: payloadTheme }),
      }).catch(() => {});

      return nextTheme;
    });
  }, []);

  return [theme, toggle];
}
