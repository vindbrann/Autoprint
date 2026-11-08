@echo off
cls
echo.
echo ========================================================
echo   AUTOPRINT SERVER - SECOURS
echo ========================================================
echo.
echo Ce script va reinitialiser le compte "admin" avec :
echo   - un mot de passe temporaire aleatoire, affiche une seule fois ci-dessous
echo   - changement force a la prochaine connexion
echo.
echo ATTENTION : Le site web doit etre arrete pour SQLite.
echo.
pause

"Autoprint.Server.exe" --reset-admin

echo.
pause
