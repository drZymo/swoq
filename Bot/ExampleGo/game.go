package main

import (
	"bot/proto/swoq"
	"context"
	"fmt"
	"time"
)

type Game struct {
	client          *swoq.GameServiceClient
	gameId          string
	mapWidth        int32
	mapHeight       int32
	visibilityRange int32
	state           *swoq.State
	seed            *int32
	replayFile      *ReplayFile
}

func (g Game) Close() error {
	return nil
}

func (g *Game) Act(action swoq.DirectedAction) error {

	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	actRequest := &swoq.ActRequest{GameId: g.gameId, Action: &action}
	actResponse, err := (*g.client).Act(ctx, actRequest)
	if err != nil {
		return err
	}

	if g.replayFile != nil {
		g.replayFile.Append(actRequest, actResponse)
	}

	if actResponse.Result != swoq.ActResult_ACT_RESULT_OK {
		return fmt.Errorf("Act failed (result %s)", actResponse.Result.String())
	}

	g.state = actResponse.State
	return nil
}
