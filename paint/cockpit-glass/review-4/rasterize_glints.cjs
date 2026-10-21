const sharp = require('C:/Users/amind/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
sharp(process.argv[2], { density: 144 })
  .resize(2048, 1024, { kernel: 'lanczos3' })
  .png()
  .toFile(process.argv[3])
  .then(info => process.stdout.write(JSON.stringify(info) + '\n'));
