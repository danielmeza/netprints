#!/usr/bin/env bash
# Removes the DataContract serializer attributes from src/NetPrints.Core (T063 part B, step 2).
#
# Deletes [DataContract], [DataMember], [KnownType] and [IgnoreDataMember] (with any arguments) from
# every attribute list, whether the list sits on its own line, shares a line with a declaration or
# combines them with other attributes ([DataMember, Foo] becomes [Foo]). Comment lines are never
# touched. Then drops "using System.Runtime.Serialization;" from a file that no longer uses anything
# from that namespace. [OnDeserializing]/[OnDeserialized] and StreamingContext are left alone.
#
# Deterministic and idempotent: a second run changes nothing. Line endings and BOMs are preserved.
# Usage: scripts/remove-datacontract.sh   (from anywhere; operates on the repository containing it)

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

find src/NetPrints.Core -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0 | sort -z |
    xargs -0 perl -e '
        use strict;
        use warnings;

        my $target = qr/^\s*(?:System\.Runtime\.Serialization\.)?(?:DataContract|DataMember|KnownType|IgnoreDataMember)(?:Attribute)?\b/;
        my $remaining_uses = qr/\b(?:StreamingContext|OnDeserializ(?:ed|ing)|OnSerializ(?:ed|ing)|EnumMember|ISerializable|SerializationInfo|SerializationException|DataContractSerializer|CollectionDataContract|OptionalField|DataContract|DataMember|KnownType|IgnoreDataMember)(?:Attribute)?\b/;

        sub split_items {
            my ($text) = @_;
            my @items;
            my $depth = 0;
            my $current = "";
            for my $ch (split //, $text) {
                if ($ch eq "," && $depth == 0) { push @items, $current; $current = ""; next; }
                $depth++ if $ch eq "(";
                $depth-- if $ch eq ")";
                $current .= $ch;
            }
            push @items, $current;
            return @items;
        }

        for my $file (@ARGV) {
            open(my $in, "<:raw", $file) or die "$file: $!";
            my $content = do { local $/; <$in> };
            close($in);

            my @out;
            for my $line (split /(?<=\n)/, $content) {
                if ($line !~ /^\s*\/\// && $line =~ /\[/) {
                    my $original = $line;
                    $line =~ s{\[([^\[\]]*)\]([ \t]*)}{
                        my ($whole, $inner, $trailing) = ($&, $1, $2);
                        my @items = split_items($inner);
                        if (grep { $_ =~ $target } @items) {
                            my @kept = grep { $_ !~ $target } @items;
                            @kept ? "[" . join(",", @kept) . "]" . $trailing : "";
                        } else {
                            $whole;
                        }
                    }ge;
                    next if $line =~ /^\s*$/ && $original !~ /^\s*$/;
                }
                push @out, $line;
            }

            my $result = join("", @out);
            my $code = join("", grep { $_ !~ /^\s*\/\// && $_ !~ /^\s*using\s+System\.Runtime\.Serialization\s*;/ } @out);
            if ($code !~ $remaining_uses) {
                $result =~ s/^[^\S\r\n]*using\s+System\.Runtime\.Serialization\s*;[^\n]*\n//m;
            }

            if ($result ne $content) {
                open(my $fh, ">:raw", $file) or die "$file: $!";
                print $fh $result;
                close($fh);
            }
        }
    ' --
