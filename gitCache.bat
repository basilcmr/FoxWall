@echo off
echo Resetting Git tracking cache...

:: Untrack everything safely
git rm -r --cached .

:: Re-add everything while respecting the updated .gitignore
git add .

echo.
echo Cache cleared and reset! 
echo Ready to commit with: git commit -m "fix: updated gitignore cache"
echo.
pause