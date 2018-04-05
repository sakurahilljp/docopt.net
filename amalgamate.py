#!python

import glob

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
        for s in usings:
            print(s, end='', file=fp)
        print('', file=fp)
        print('namespace DocoptNet', file=fp)
        print('{', file=fp)
        for s in codes:
            print(s, end='', file=fp)
        print('}', file=fp)

if __name__ == '__main__':
    main()