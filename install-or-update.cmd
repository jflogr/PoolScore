@echo off
cd /d "%~dp0"
start "" "http://localhost:4174"
node server.mjs
