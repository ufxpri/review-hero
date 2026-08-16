// 효과음 목록 — 이 게임이 내는 소리 전부.
//
// 이 게임은 **리뷰가 무기**이고 미술은 커머스 패러디 + 장부다. 그래서 소리도 판타지가 아니라
// **사무실·물류·문구류**다 — 종이, 펜, 도장, 테이프, 동전. 검이 부딪히는 소리는 여기 없다.
//
// 텍스트를 읽는 게임이라 소리가 시끄러우면 읽기를 방해한다. 전부 짧고(20ms~900ms) 낮다.

namespace ReviewHero.Game.Audio;

public enum SfxId
{
    /// <summary>카드 집기 — 종이 한 장이 스친다</summary>
    CardPick,

    /// <summary>카드 놓기 — 집었다 그냥 내려놓았다</summary>
    CardDrop,

    /// <summary>서명 (짧은 획) — 펜촉이 종이를 긁는다</summary>
    SignatureShort,

    /// <summary>서명 (기본 획) — 카드 제출 연출의 0.40초에 맞춘 길이</summary>
    Signature,

    /// <summary>서명 (긴 획)</summary>
    SignatureLong,

    /// <summary>제출 — 종이가 상품 쪽으로 날아가는 휙</summary>
    CardThrow,

    /// <summary>판정 도장 · 원산지 — 묵직하게 눌러 찍고 낮게 울린다</summary>
    StampOrigin,

    /// <summary>판정 도장 · 팩트 — 딱 떨어진다</summary>
    StampFact,

    /// <summary>판정 도장 · 일반 — 밋밋하다. 소리도 작다</summary>
    StampNormal,

    /// <summary>판정 도장 · 헛소리/빗나감 — 김빠지는 소리</summary>
    StampFumble,

    /// <summary>좋아요 적중 — 짧은 클릭. 좋아요 수만큼 피치가 살짝 오른다 (<see cref="Sfx.Like"/>)</summary>
    Like,

    /// <summary>베스트 리뷰(크리) — 도장 + 상승하는 종소리</summary>
    Crit,

    /// <summary>피격 — 둔탁한 저역 타격</summary>
    Hurt,

    /// <summary>방어로 막음 — 피격보다 짧고 높다</summary>
    Block,

    /// <summary>전투 승리 — 짧은 상승 시퀀스</summary>
    Win,

    /// <summary>전투 패배 — 짧은 하강 시퀀스</summary>
    Lose,

    /// <summary>버튼 — 아주 작은 클릭</summary>
    Click,

    /// <summary>토스트 안내 — 아주 작은 틱</summary>
    Toast,

    /// <summary>택배 개봉 — 테이프를 뜯는다</summary>
    Parcel,

    /// <summary>퇴고·파쇄·소각 — 종이를 구긴다</summary>
    Crumple,

    /// <summary>골드 — 동전</summary>
    Coin,
}
