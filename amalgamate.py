#!python

import glob

LICENSE='./LICENSE-MIT'
PATTERN='./src/DocoptNet/*.cs'
OUTPUT='Docopt.cs'


def main():
    usings = set();
    codes = []

    for srcfile in glob.glob(PATTERN):
        #print(srcfile)
        with open(srcfile, 'r', encoding='utf-8-sig') as fp:
            for line in fp:
                #print(line, end='')
                if line.startswith('using'):
                   usings.add(line)
                elif line.startswith('namespace'):
                    pass
                elif line.startswith('{') or line.startswith('}'):
                    pass
                else:
                    codes.append(line)  

    with open(OUTPUT, 'w') as fp:
        # License
        print('''/*

Docopt.NET (https://github.com/docopt/docopt.net)

Copyright (c) 2012 Vladimir Keleshev, <vladimir@keleshev.com>
                   Dinh Doan Van Bien, <dinh@doanvanbien.com>

Licensed under MIT (https://github.com/docopt/docopt.net/blob/master/LICENSE-MIT)

*/

''', file=fp)
        
        # using directive
        for s in sorted(usings):
            print(s, end='', file=fp)
        print('', file=fp)

        # implementations
        print('namespace DocoptNet', file=fp)
        print('{', file=fp)
        for s in codes:
            print(s, end='', file=fp)
        print('}', file=fp)

if __name__ == '__main__':
    main()