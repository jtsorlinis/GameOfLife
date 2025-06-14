var ghpages = require('gh-pages');

ghpages.publish('../Build/GameOfLife',{
  remove: ['.','.*', ".vscode"]
},(err) =>{
  console.log(err);
});