@echo off

IF %1=="UPDATE" GOTO RunUpdate
copy node-upgrade.bat ..\
cd ..
start node-upgrade.bat "UPDATE" %1 & exit
GOTO Done

:RunUpdate
timeout /t 3
echo Stopping FileFlows Node if running
taskkill %2

echo.
echo Removing previous version
rmdir /q /s Node

echo.
echo Copying Node update files
rename NodeUpdate Node

echo.
echo Starting FileFlows Node
start run-node.bat

if exist node-upgrade.bat goto Done
del node-upgrade.bat & exit

:Done
exit
