package main

import (
	"bot/proto/swoq"
	"context"
	"fmt"
	"log"
	"time"

	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

type GameConnection struct {
	userId        string
	userName      string
	replaysFolder string
	conn          *grpc.ClientConn
	client        swoq.GameServiceClient
}

func NewGameConnection(host string, userId string, userName string, replaysFolder string) (*GameConnection, error) {
	conn, err := grpc.NewClient(host, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		return nil, err
	}
	client := swoq.NewGameServiceClient(conn)

	return &GameConnection{userId: userId, userName: userName, replaysFolder: replaysFolder, conn: conn, client: client}, nil
}

func (g GameConnection) Close() error {
	return g.conn.Close()
}

func (g GameConnection) Start(level *int32, seed *int32) (*Game, error) {
	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	startRequest := &swoq.StartRequest{UserId: g.userId, UserName: g.userName, Level: level, Seed: seed}

	startResponse, err := g.client.Start(ctx, startRequest)
	if err != nil {
		return nil, err
	}
	for startResponse.Result == swoq.StartResult_START_RESULT_QUEST_QUEUED {
		log.Printf("Quest queued, retrying ...")
		startResponse, err = g.client.Start(ctx, startRequest)
		if err != nil {
			return nil, err
		}
	}
	if startResponse.Result != swoq.StartResult_START_RESULT_OK {
		return nil, fmt.Errorf("Start failed (result %s)", startResponse.Result.String())
	}

	var replayFile *ReplayFile = nil
	if g.replaysFolder != "" {
		var err error
		replayFile, err = NewReplayFile(g.replaysFolder, startRequest, startResponse)
		if err != nil {
			return nil, err
		}
	}

	return &Game{
		client:          &g.client,
		gameId:          *startResponse.GameId,
		mapWidth:        *startResponse.MapWidth,
		mapHeight:       *startResponse.MapHeight,
		visibilityRange: *startResponse.VisibilityRange,
		state:           startResponse.State,
		seed:            startResponse.Seed,
		replayFile:      replayFile,
	}, nil
}
