# TmDocumentEditor: interní pravidla pro obrázky a obtékání

## Zdroj pravdy

Zdroj pravdy pro obtékání textu kolem obrázků je intervalový layout engine, ne CSS `float`, `clear` ani `shape-outside`.

WYSIWYG text layer vykresluje textové řádky podle vypočtených dostupných intervalů. Object layer vykresluje obrázky podle layout rectů. Text layer proto nesmí rezervovat browser flow pro floating objekty pomocí starých placeholderů, sidecar odstavců, float utility tříd ani center full-band fallbacku.

## Inline vs Anchored/Floating

`Inline` obrázek je skutečná inline kresba v toku textu. Jeho anchor v text layer má reálnou šířku a výšku a chová se jako znakový objekt uvnitř řádku. Inline obrázek tedy smí ovlivnit výšku řádku a pozici okolního textu přímo v textovém toku.

`Anchored` nebo `Floating` obrázek má v text layer jen nulový anchor. Anchor drží identitu objektu a polohu v dokumentovém modelu, ale nerezervuje prostor pro prohlížeč. Viditelný obrázek žije v object layer a textové řádky se mu vyhýbají přes intervaly vypočtené layout enginem.

## Square vs TopBottom

`Square` vytváří blokované intervaly podle skutečného obdélníku objektu a jeho vzdáleností od textu. Pokud je obrázek uprostřed řádku, layout musí umět vrátit levý i pravý dostupný interval, ne přepsat scénář na full-band rezervaci.

`TopBottom` je režim, který záměrně blokuje celý vodorovný pás objektu. Text tedy pokračuje až nad nebo pod objektem. Tenhle režim je explicitní volba uživatele, ne fallback pro centrovaný `Square`.

## Legacy image blocky

Demo dokumenty pro editor mají používat `DocumentDrawingRun` uvnitř odstavců. Top-level `ImageBlockContent` může zůstat jen jako importní nebo statický fallback mimo WYSIWYG wrap flow. Pokud se takový legacy image block dostane do WYSIWYG text layer, nesmí se z něj stát zdroj obtékání; editor jej musí buď převést na drawing run na hranici persistence/importu, nebo vykreslit odděleně jako fallback bez CSS flow hacků.
