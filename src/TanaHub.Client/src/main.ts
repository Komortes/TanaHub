import { mount } from 'svelte';
import App from './App.svelte';
import './app.css';
import { bootstrapTheme } from './lib/theme';

bootstrapTheme();
mount(App, { target: document.getElementById('app')! });
