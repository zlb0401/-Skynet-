@echo off
chcp 65001 >nul
echo === Skynet Card Battle Test Client ===
where py >nul 2>&1
if %errorlevel%==0 (
    py -3 "%~dp0test_client.py" --host 127.0.0.1 --port 8888 --user test --password 123456
    goto :done
)
where python >nul 2>&1
if %errorlevel%==0 (
    python "%~dp0test_client.py" --host 127.0.0.1 --port 8888 --user test --password 123456
    goto :done
)
echo ERROR: Python not found. Install from https://www.python.org/downloads/
:done
pause
