/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: [
    './Views/**/*.cshtml',
    './wwwroot/**/*.js'
  ],
  theme: {
    extend: {
      colors: {
        'gv-dark':   '#061f17',
        'gv-deep':   '#0a3124',
        'gv-mid':    '#115740',
        'gv-accent': '#23a476',
        'gv-light':  '#f2f7f5',
        'gv-sage':   '#d1e8df',
        'emerald-green': '#10b981',
      },
      fontFamily: {
        sans: ['Inter', 'Segoe UI', 'Roboto', 'sans-serif'],
      },
    },
  },
  corePlugins: {
    preflight: false,
  },
  plugins: [],
}
