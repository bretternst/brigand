xbuild Brigand/Brigand.csproj /p:Configuration=Release
xbuild BrigandCmd/BrigandCmd.csproj /p:Configuration=Release
xbuild Trivia/Trivia.csproj /p:Configuration=Release

mkdir -p build/
mkdir -p build/Modules

cp -f BrigandCmd/bin/Release/*.exe build/
cp -f BrigandCmd/bin/Release/*.dll build/
cp -f Trivia/bin/Release/Brigand.Trivia.dll build/Modules/
cp -f BrigandCmd/init.py build/
cp -f External/*.dll build/

cp -f BrigandCmd/bin/Release/*.exe build-gb/
cp -f BrigandCmd/bin/Release/*.dll build-gb/
cp -f Trivia/bin/Release/Brigand.Trivia.dll build-gb/Modules/
cp -f BrigandCmd/init.py build-gb/
cp -f External/*.dll build-gb/
