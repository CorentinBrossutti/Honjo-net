You just downloaded the util library of the honjo-net framework.
This library is not really meant for usage on its own albeit it provides some useful utilitaries, namely pseudo-enums
which are classes treated as enums where you can add values at runtime. The library will be more complete in the future.
See https://github.com/Reymmer/honjo-net for informations and wikis.

Most useful classes :
  - PseudoEnum : base class for pseudo enums
  - GenPseudoEnum<T> : use like this -> public class MyPseudoEnum : GenPseudoEnum<MyPseudoEnum>
    It allows you to use MyPseudoEnum.Get(id) without using any cast and without overriding base methods