namespace jp.ootr.ludo
{
    public enum GamePhase
    {
        Idle,
        Lobby,
        DetermineOrder,
        TurnBegin,
        Rolling,
        SelectMove,
        ResolvingMove,
        TurnEnd,
        GameEnd
    }

    public enum TokenState
    {
        Yard,
        Track,
        HomeRow,
        Home
    }

    public enum EndRuleMode
    {
        FirstPlaceOnly,
        FullRanking
    }

    public enum PlayerColor
    {
        Red,
        Blue,
        Yellow,
        Green
    }
}
