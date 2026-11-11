# Gel and held-tool visuals — 0.12.1

Gel reach doubles: 48 logical units around incision sections, and 44 around fragment cuts. Green application particles now have twice the side length (18–34 units), with twice the small scatter radius. Dose, dispense rate, saved coverage and healing remain the same.

The persistent sea-green disinfection overlay is shown only during the incision phase. It disappears when the main incision is finished; coverage remains saved, still affects infection risk, and remains readable in the header. Fresh gel application can still emit short-lived particles later in the operation.

Held Forceps now receive the same thin fading red trail as Suture and Scalpel. Gel keeps its green effect. Suture distance continues to count only the Suture tool, not forceps motion.

The knife contact mapping is intentionally unchanged pending the user's annotation. Exported local handSharp.png and handSharpalt.png are the original 256 x 630 transparent sprite images, with no scaling or drawn markers. They are supplied locally for annotation only and excluded from the repository/build package. In-game mirroring and UI scaling will be checked against the marked contact point in the next adjustment.

Playtest: hold gel near a wound edge to confirm the larger reach; finish an incision and confirm green prep markers disappear without losing coverage; hold forceps and verify the red trail, with no extra suture distance counted. The knife alignment issue is still open.
