node('linux'){
    String args = '';
    if (branch == 'master') {
        args += ' -nuget'
    }
    sh "/bin/bash build.sh release ${args.trim()}"
}