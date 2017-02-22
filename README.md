# Uploadr.Net
Flickr Uploadr.Net - Commandline for batch-uploading to Flickr

The GUI is currently not implemented. I found the commandline tool to be very flexible, making the GUI obsolete (use the flickr uploader, if you need interactive uploading).

Here's an example of how to use the commandline:

First, get an authentication-token from Flickr, which the commandline can use to interact with your flickr account:

    UploadrNet --authenticate --key auth.key
    
This will start your browser and ask you to logon with your yahoo-account and then wether you want to grant Uploadr.Net WRITE access to your account. If you choose so, the Flickr will show you a CODE in form `xxx-xxx-xxx`, which you must enter at the prompt. Uploadr.Net will verify your input with the Flickr API and upon success will write the authentication token to the file specified (auth.key in this case).

Now you're ready to upload to your account:

    UploadrNet --process --source "c:\Path\To\My\Photo\Library" --desc "{relpath}" --tags "{relpathastags},another tag" --updatedups --key auth.key --album {relrootfolder} --createAlbums --family --search Hidden 
    
This will do the following:
* scan all folders within `--source` for any supported image and video formats
* set the filename as the title (because we don't specify otherwise)
* set the relative path (everything after the `source` path) (`{relpath}` expression) as the description (`--desc` argument)
* split the relative path into a comma-separated list (`{relpathastags}` expression) and add the folder names as individual tags (`--tags` argument), and also add a static tag `another tag` (tags are separated by comma, so they are replaced with a space in directory names)
* add the media file to the album (`--album` argument) with the same name as the first foldername below the root folder (`{relrootfolder}` expression), creating the album, if it doesn't exist (`--createAlbums` argument)
* set the visibility to `family` only (`--family` argument, versus `--public` or `--friends`)
* make the media file `hidden` in flickr search (`--search` argument)
* use the key (`auth.key`) generated in the `--authenticate` step (`--key` argument)

There are a couple of expansion-expressions, which can be used for `tags`, `title`, or the `description`
(Examples assume `--source c:\Images` was supplied and the file's path was `c:\Images\Family\2014\Summer Vacation\image01.jpg`)

Expression | Description
--- | ---
`{now}` | the current date/time in ISO format
`{fname}` | the filename of the media file (ie `image01`)
`{fnameandext}` | the filename with extension of the media file (ie `image01.jpg`)
`{folder}` | the immediate folder name of the media file (ie `Summer Vacation`)
`{path}` | the complete path (without filename) of the media file (ie. `c:\Images\Family\2014\Summer Vacation`)
`{relrootfolder}` | the first folder  relative from the `root` folder specified (ie. `Family`)
`{relpath}` | the path relative from the `root` folder specified (ie. `Family\2014\Summer Vacation`)
`{relpathastags}` | the path relative from the `root` folder specified, in `tag` notation (ie. `Family,2014,Summer Vacation`)
