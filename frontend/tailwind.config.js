/** @type {import('tailwindcss').Config} */
import forms from '@tailwindcss/forms';

export default {
  content: [
    "./index.html",
    "./src/**/*.{vue,js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        tactical: {
          bg: '#050811',
          surface: '#070b16',
          panel: '#0c1222',
          border: '#152238',
          cyan: '#00f0ff',
          amber: '#ffb703',
          alert: '#ff0055',
          online: '#00ff66',
          offline: '#ff0055',
          suppressed: '#8b5cf6',
          unknown: '#64748b'
        }
      },
      fontFamily: {
        mono: ['JetBrains Mono', 'Consolas', 'Courier New', 'monospace']
      }
    },
  },
  plugins: [forms],
};
