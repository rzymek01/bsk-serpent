using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace serpent {
    interface IAlgorithm : IDisposable {
        /**
         * w konstruktorze należy podać klucz sesyjny oraz wektor IV
         */

        bool Encryption { get; }

        Int64 getSrcLength();

        /**
         * @param srcFile plik wejściowy
         * @param dstFile plik wyjściowy
         * @param opMode tryb szyfrowania: ECB, CBC, CFB, OFB
         * @param segmentSize rozmiar podbloku dla wybranego trybu szyfrowania
         * @param srcFileOffset początkowe przesunięcie wskaźnika odczytu w pliku wejściowym
         * @param dstFileOffset początkowe przesunięcie wskaźnika zapisu w pliku wyjściowym
         * @throws System.ArgumentException
         */
        void init(String srcFile, String dstFile, String opMode, int segmentSize,
                Int64 srcFileOffset = 0, Int64 dstFileOffset = 0);

        /**
         * @param minBytes minimalna/szacunkowa liczba bajtów do przetworzenia
         * @returns liczba faktycznie przetworzonych bajtów
         */
        Int64 encrypt(Int64 minBytes);

        /**
         * @param minBytes minimalna/szacunkowa liczba bajtów do przetworzenia
         * @returns liczba faktycznie przetworzonych bajtów
         */
        Int64 decrypt(Int64 minBytes);
    }
}
