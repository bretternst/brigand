msbuild Brigand/Brigand.csproj /p:Configuration=Release
msbuild BrigandCmd/BrigandCmd.csproj /p:Configuration=Release
msbuild Trivia/Trivia.csproj /p:Configuration=Release
msbuild Chatter/Chatter.csproj /p:Configuration=Release
msbuild RssWatcher/RssWatcher.csproj /p:Configuration=Release

mkdir build
mkdir build\Modules

copy /Y BrigandCmd\bin\Release\*.exe build\
copy /Y BrigandCmd\bin\Release\*.dll build\
copy /Y Trivia\bin\Release\Brigand.Trivia.dll build\Modules\
copy /Y Chatter\bin\Release\Brigand.Chatter.dll build\Modules\
copy /Y RssWatcher\bin\Release\Brigand.RssWatcher.dll build\Modules\
REM copy /Y BrigandCmd\bot.config build\
copy /Y BrigandCmd\init.py build\
copy /Y External\*.dll build\
