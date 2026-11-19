import {defineConfig} from '@playwright/test';
import fs from 'node:fs';
const brave='/Applications/Brave Browser.app/Contents/MacOS/Brave Browser';
export default defineConfig({testDir:'./pruebas-piloto-ui',workers:1,timeout:60000,use:{baseURL:'http://127.0.0.1:5097',launchOptions:fs.existsSync(brave)?{executablePath:brave}:{}},reporter:[['list']],outputDir:'../.local/pilot-tests/ui'});
