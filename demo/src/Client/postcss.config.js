const purgecss = require('@fullhuman/postcss-purgecss')({
  // Specify the paths to all of the template files in your project
  whitelist: [ 'body' ],
  content: [
    './src/**/*.fs',
  ],
  // Include any special characters you're using in this regular expression
  defaultExtractor: content => content.match(/[\w-/:]+(?<!:)/g) || []
});

const cssnano = require('cssnano')({
  preset: ['default', {
    discardComments: { removeAll: true }
  }]
});

const isProduction = !process.argv.find(v => v.indexOf('webpack-dev-server') !== -1);

module.exports = {
  plugins: isProduction ? [
    require('tailwindcss'),
    require('autoprefixer'),
    purgecss,
    cssnano
  ] : [
    require('tailwindcss'),
    require('autoprefixer')
  ]
};
